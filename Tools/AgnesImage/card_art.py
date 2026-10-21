"""Believer 卡图批处理生成器（Agnes 文生图 / 图生图）。

读取 ``docs/M10.2.1-CardArt-Spec.csv``（GB18030），为当前代码的 64 张卡生成正式卡图：

- 40 张普通卡：3 Basic + 21 Common + 16 Uncommon；
- 12 张普通神迹（Rare，卡组 ``普通神迹``）；
- 12 张强化神迹（Token，卡组 ``强化神迹``，由对应普通神迹行的
  ``needs_enhanced_art=是`` + ``enhanced_card_id`` 派生，slug 形如 ``enhanced_*``）。

输出契约（与游戏卡图目录一致）::

    big   : 1000x760 RGB PNG（cover-fit 裁切，LANCZOS）
    small : 250x190 RGB PNG（由 big 直接缩小）

默认暂存目录 ``Tools/AgnesImage/out/card_art/{big,small}``；
``--install`` 时写入 ``Believer/images/card_portraits/big`` 与
``Believer/images/card_portraits``（同名小图放根目录）。

Agnes 偶尔会返回带浅灰留白条的画布（例如 1152x864 里只有中间 610x864 是画），
本脚本会在裁切前检测并去掉这种边框留白（见 ``detect_content_bbox`` /
``normalize_artwork``），再把结果 cover-fit 到 1000x760。

用法::

    python card_art.py --all --dry-run                  # 只列任务，不调用 API
    python card_art.py --only BELIEVER-FOCUS --staging  # 生成单张到暂存目录
    python card_art.py --all --concurrency 2            # 全量生成（注意配额）
    python card_art.py --all --install                  # 直接写入游戏卡图目录
    python card_art.py --only focus --force --sheet     # 重生成并出接触表

注意：本脚本不会打印 API key；密钥由 ``agnes_client`` 从 ``Tools/AgnesImage/.env`` 读取。
"""

from __future__ import annotations

import argparse
import csv
import io
import json
import os
import re
import shutil
import sys
import tempfile
import textwrap
import threading
import time
from collections import Counter
from concurrent.futures import ThreadPoolExecutor, as_completed
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Any, Iterable, Mapping

from PIL import Image, ImageDraw, ImageFont, ImageOps, ImageStat

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]  # Tools/AgnesImage -> 仓库根目录
if str(HERE) not in sys.path:  # 允许以任意 cwd 运行
    sys.path.insert(0, str(HERE))

import agnes_client  # noqa: E402  （已测试的 Agnes 客户端，禁止修改）

import believer_style  # noqa: E402  （本目录的画风/提示词模块）

# ---------------------------------------------------------------------------
# 常量
# ---------------------------------------------------------------------------

SPEC_CSV = ROOT / "docs" / "M10.2.1-CardArt-Spec.csv"
STAGING_DIR = HERE / "out" / "card_art"
INSTALL_DIR = ROOT / "Believer" / "images" / "card_portraits"
DEFAULT_MANIFEST = HERE / "out" / "manifest.jsonl"
DEFAULT_SHEET = STAGING_DIR / "contact_sheet.png"

BIG_SIZE: tuple[int, int] = (1000, 760)
SMALL_SIZE: tuple[int, int] = (250, 190)
DEFAULT_REQUEST_SIZE = "1024x768"

# 与规格表 card_group 对应的分组
NORMAL_GROUPS = frozenset({"初始卡", "白卡", "蓝卡"})
MIRACLE_GROUP = "普通神迹"
ENHANCED_GROUP = "强化神迹"
NEEDS_ENHANCED_YES = "是"

# API / 批处理参数
RETRIES = 5  # 队列满（HTTP 503 image queue is full）时客户端按 2/4/8/16/32s 重试
DEFAULT_CONCURRENCY = 2
DEFAULT_RPM = 20  # 免费档 RPM 上限约 20；可用 --rpm / AGNES_RPM 调整
EXPECTED_JOB_COUNT = 64

# 留白条检测阈值（Agnes 返回的留白条通常是 238-243 的均匀浅灰）
PAD_MEAN_MIN = 225.0
PAD_STD_MAX = 25.0
MIN_TRIM_PX = 2
MAX_BAR_RATIO = 0.45
MIN_CONTENT_AREA_RATIO = 0.40
MIN_CONTENT_LUMA = 200  # 整幅都高亮的合法画面不当作留白裁切

# 参考图上传统一缩放到的长边上限（避免 Data URI 过大触发 HTTP 413）
REF_MAX_EDGE = 768

VARIANT_LABEL_ZH = {"normal": "普通", "miracle": "神迹", "enhanced": "强化"}

EXAMPLES = """示例:
  python card_art.py --all --dry-run
  python card_art.py --only BELIEVER-FOCUS --staging
  python card_art.py --only BELIEVER-FOCUS,enhanced_let_there_be_light --force
  python card_art.py --all --concurrency 2 --manifest out/manifest.jsonl
  python card_art.py --all --install
  python card_art.py --only focus --sheet
"""


class InputError(RuntimeError):
    """输入/环境错误（返回码 2）。"""


# ---------------------------------------------------------------------------
# 规格表与任务
# ---------------------------------------------------------------------------


@dataclass
class Job:
    """一个卡图生成任务。"""

    job_id: str  # 卡牌 ID：普通/神迹任务用自身 card_id，强化任务用 enhanced_card_id
    slug: str  # 输出文件名（去掉 BELIEVER- 前缀并小写）
    variant: str  # normal / miracle / enhanced
    row: Mapping[str, str]  # 用于元数据的规格行（强化任务为强化神迹行）
    subject_row: Mapping[str, str]  # 用于提示词主体的规格行（强化任务为对应普通神迹行）
    prompt: str
    big_path: Path
    small_path: Path


def load_spec_rows(path: Path) -> list[dict[str, str]]:
    """读取卡图规格表（GB18030）。"""
    if not path.is_file():
        raise InputError(f"找不到卡图规格表: {path}")
    try:
        with path.open(encoding="gb18030", newline="") as handle:
            reader = csv.DictReader(handle)
            rows = [{key: (value or "") for key, value in row.items() if key is not None} for row in reader]
    except UnicodeDecodeError as exc:
        raise InputError(f"卡图规格表解码失败（应为 GB18030）: {exc}") from exc
    except csv.Error as exc:
        raise InputError(f"卡图规格表解析失败: {exc}") from exc

    rows = [row for row in rows if (row.get("card_id") or "").strip()]
    if not rows:
        raise InputError(f"卡图规格表没有数据行: {path}")
    return rows


def make_slug(card_id: str) -> str:
    """卡牌 ID -> 卡图文件名 slug：去 ``BELIEVER-`` 前缀并小写。"""
    value = card_id.strip()
    if value.upper().startswith("BELIEVER-"):
        value = value[len("BELIEVER-") :]
    slug = value.strip().lower()
    if not slug:
        raise InputError(f"无法从卡牌 ID 推导 slug: {card_id!r}")
    if not re.fullmatch(r"[a-z0-9_]+", slug):
        raise InputError(f"卡牌 ID 推导出的 slug 含非法字符: {card_id!r} -> {slug!r}")
    return slug


def _res_slug(value: str) -> str:
    """从 ``res://.../slug.png`` 取文件名（不含扩展名）。"""
    name = (value or "").strip().rsplit("/", 1)[-1]
    if name.lower().endswith(".png"):
        name = name[: -len(".png")]
    return name


def _make_job(
    card_id: str,
    variant: str,
    row: Mapping[str, str],
    subject_row: Mapping[str, str],
    *,
    root: Path,
    install: bool,
) -> Job:
    slug = make_slug(card_id)
    big_path = root / "big" / f"{slug}.png"
    small_dir = root if install else root / "small"
    return Job(
        job_id=card_id,
        slug=slug,
        variant=variant,
        row=row,
        subject_row=subject_row,
        prompt=believer_style.build_prompt(subject_row, variant=variant),
        big_path=big_path,
        small_path=small_dir / f"{slug}.png",
    )


def build_jobs(rows: Iterable[Mapping[str, str]], *, root: Path, install: bool) -> tuple[list[Job], list[str]]:
    """把规格行展开成 64 个任务（普通 40 + 普通神迹 12 + 强化神迹 12）。

    强化任务由普通神迹行派生：``needs_enhanced_art=是`` 时用 ``enhanced_card_id``
    生成第二张图（variant=enhanced），提示词主体沿用普通神迹行。
    """
    rows = list(rows)
    enhanced_rows = {
        (row.get("card_id") or "").strip(): row
        for row in rows
        if (row.get("card_group") or "").strip() == ENHANCED_GROUP
    }

    jobs: list[Job] = []
    warnings: list[str] = []

    for row in rows:
        card_id = (row.get("card_id") or "").strip()
        group = (row.get("card_group") or "").strip()
        if not card_id:
            continue

        if group in NORMAL_GROUPS:
            jobs.append(_make_job(card_id, "normal", row, row, root=root, install=install))
        elif group == MIRACLE_GROUP:
            jobs.append(_make_job(card_id, "miracle", row, row, root=root, install=install))
            if (row.get("needs_enhanced_art") or "").strip() == NEEDS_ENHANCED_YES:
                enhanced_id = (row.get("enhanced_card_id") or "").strip()
                if not enhanced_id:
                    raise InputError(f"普通神迹 {card_id} 标记需要强化图，但 enhanced_card_id 为空")
                enhanced_row = enhanced_rows.get(enhanced_id)
                if enhanced_row is None:
                    warnings.append(f"规格表缺少强化行 {enhanced_id}，回退用普通神迹行生成提示词")
                    enhanced_row = row
                jobs.append(
                    _make_job(
                        enhanced_id,
                        "enhanced",
                        enhanced_row,
                        row,  # 主体/构图与普通神迹一致，只增强光效
                        root=root,
                        install=install,
                    )
                )
        elif group == ENHANCED_GROUP:
            # 已由对应普通神迹行派生，避免重复
            continue
        else:
            warnings.append(f"未知卡组 {group!r}：{card_id}（未生成任务）")

    # 校验 slug 与规格表 expected_*_art 路径一致，及时暴露文档漂移
    for job in jobs:
        expected_big = _res_slug(job.row.get("expected_big_art", ""))
        expected_small = _res_slug(job.row.get("expected_small_art", ""))
        if expected_big and expected_big != job.slug:
            warnings.append(f"{job.job_id}: slug {job.slug!r} 与 expected_big_art {expected_big!r} 不一致")
        if expected_small and expected_small != job.slug:
            warnings.append(f"{job.job_id}: slug {job.slug!r} 与 expected_small_art {expected_small!r} 不一致")

    return jobs, warnings


def _job_keys(job: Job) -> set[str]:
    card_id = job.job_id.upper()
    keys = {card_id, job.slug.lower()}
    if card_id.startswith("BELIEVER-"):
        keys.add(card_id[len("BELIEVER-") :])
    return keys


def _selector_keys(selector: str) -> set[str]:
    value = selector.strip()
    if not value:
        return set()
    keys = {value, value.upper(), value.lower()}
    if value.upper().startswith("BELIEVER-"):
        keys.add(value.upper()[len("BELIEVER-") :])
    return keys


def _job_matches(job: Job, selectors: Iterable[str]) -> bool:
    keys = _job_keys(job)
    return any(keys & _selector_keys(selector) for selector in selectors)


def _parse_selectors(raw: Iterable[str]) -> list[str]:
    selectors: list[str] = []
    for item in raw:
        selectors.extend(part.strip() for part in str(item).split(",") if part.strip())
    return selectors


# ---------------------------------------------------------------------------
# 图片后处理：留白条检测 + cover-fit
# ---------------------------------------------------------------------------


def _padding_flags(image: Image.Image, axis: str) -> list[bool]:
    """逐列/逐行统计：均值足够亮且标准差足够小 -> 视为留白条。"""
    width, height = image.size
    flags: list[bool] = []
    if axis == "x":
        for x in range(width):
            stat = ImageStat.Stat(image.crop((x, 0, x + 1, height)))
            flags.append(min(stat.mean) > PAD_MEAN_MIN and max(stat.stddev) < PAD_STD_MAX)
    else:
        for y in range(height):
            stat = ImageStat.Stat(image.crop((0, y, width, y + 1)))
            flags.append(min(stat.mean) > PAD_MEAN_MIN and max(stat.stddev) < PAD_STD_MAX)
    return flags


def _edge_runs(flags: list[bool]) -> tuple[int, int, int]:
    """返回 (首部连续留白长度, 尾部连续留白长度, 留白总数)。"""
    total = sum(flags)
    head = 0
    while head < len(flags) and flags[head]:
        head += 1
    tail = 0
    while tail < len(flags) and flags[-1 - tail]:
        tail += 1
    return head, tail, total


def _luma_min(image: Image.Image) -> int:
    return int(image.convert("L").getextrema()[0])


def detect_content_bbox(image: Image.Image) -> tuple[tuple[int, int, int, int], dict[str, Any]]:
    """检测浅色留白条并返回内容 bbox（left, top, right, bottom，右/下不含）。

    只有同时满足以下条件才裁切，否则原样返回整幅图：

    - 留白只出现在四条边（内部不存在均匀亮条）；
    - 至少裁掉 2px；
    - 单侧留白深度不超过该边长的 45%；
    - 剩余内容面积不小于原图的 40%；
    - 内容不是整体高亮（保护合法的全亮画面）。
    """
    width, height = image.size
    meta: dict[str, Any] = {
        "original_size": [width, height],
        "content_bbox": [0, 0, width, height],
        "bars_trimmed": False,
        "trim_px": [0, 0, 0, 0],
    }
    full_box = (0, 0, width, height)

    x_flags = _padding_flags(image, "x")
    y_flags = _padding_flags(image, "y")
    left, right, total_x = _edge_runs(x_flags)
    top, bottom, total_y = _edge_runs(y_flags)

    # 内部也有“亮而均匀”的列/行时，说明不是边框留白，放弃裁切
    if total_x > left + right or total_y > top + bottom:
        meta["skip_reason"] = "interior_uniform_bright"
        return full_box, meta

    if left + right < MIN_TRIM_PX and top + bottom < MIN_TRIM_PX:
        return full_box, meta

    content_w = width - left - right
    content_h = height - top - bottom
    if content_w <= 0 or content_h <= 0:
        meta["skip_reason"] = "no_content"
        return full_box, meta

    if (
        left > width * MAX_BAR_RATIO
        or right > width * MAX_BAR_RATIO
        or top > height * MAX_BAR_RATIO
        or bottom > height * MAX_BAR_RATIO
    ):
        meta["skip_reason"] = "bar_too_deep"
        return full_box, meta

    if content_w * content_h < width * height * MIN_CONTENT_AREA_RATIO:
        meta["skip_reason"] = "content_too_small"
        return full_box, meta

    cropped = image.crop((left, top, width - right, height - bottom))
    if _luma_min(cropped) >= MIN_CONTENT_LUMA:
        meta["skip_reason"] = "all_bright_content"
        return full_box, meta

    meta.update(
        {
            "content_bbox": [left, top, width - right, height - bottom],
            "bars_trimmed": True,
            "trim_px": [left, top, right, bottom],
        }
    )
    return (left, top, width - right, height - bottom), meta


def normalize_artwork(png_bytes: bytes, *, size: tuple[int, int] = BIG_SIZE) -> tuple[Image.Image, dict[str, Any]]:
    """把 Agnes 返回的图片规整为 ``size`` 的 RGB 图（先去留白条，再 cover-fit）。

    :return: ``(PIL.Image, meta)``；meta 含 original_size / content_bbox /
             bars_trimmed / trim_px / normalized_size。
    """
    try:
        with Image.open(io.BytesIO(png_bytes)) as opened:
            source = opened.convert("RGB")
    except Exception as exc:  # PIL 无法解码时给出明确错误
        raise RuntimeError(f"生成结果不是可解码的图片: {exc}") from exc

    bbox, meta = detect_content_bbox(source)
    cropped = source.crop(bbox) if meta["bars_trimmed"] else source
    normalized = ImageOps.fit(
        cropped,
        size,
        method=Image.Resampling.LANCZOS,
        centering=(0.5, 0.5),
    )
    normalized = normalized.convert("RGB")
    meta["normalized_size"] = [size[0], size[1]]
    return normalized, meta


def _atomic_save_png(image: Image.Image, path: Path) -> None:
    """先写 .tmp 再原子替换，Ctrl-C 不会留下半张图。"""
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_name(path.name + ".tmp")
    try:
        image.save(tmp, format="PNG", optimize=True)
        os.replace(tmp, path)
    finally:
        if tmp.exists():
            tmp.unlink(missing_ok=True)


def output_is_valid(job: Job) -> bool:
    """已存在的输出是否可跳过：PNG、尺寸正确、RGB。"""
    for path, expected in ((job.big_path, BIG_SIZE), (job.small_path, SMALL_SIZE)):
        if not path.is_file():
            return False
        try:
            with Image.open(path) as image:
                if image.format != "PNG" or image.size != expected or image.mode != "RGB":
                    return False
        except Exception:
            return False
    return True


# ---------------------------------------------------------------------------
# 并发 / 限速 / manifest
# ---------------------------------------------------------------------------


class RateLimiter:
    """简单的匀速限速器（所有线程共享）。"""

    def __init__(self, rpm: int) -> None:
        if rpm < 1:
            raise InputError(f"RPM 必须是正整数: {rpm}")
        self.interval = 60.0 / rpm
        self._next_at = 0.0
        self._lock = threading.Lock()

    def wait(self) -> None:
        with self._lock:
            now = time.monotonic()
            delay = max(0.0, self._next_at - now)
            self._next_at = max(now, self._next_at) + self.interval
        if delay > 0:
            time.sleep(delay)


class ManifestWriter:
    """JSONL manifest（线程安全，逐条追加并立即落盘）。"""

    def __init__(self, path: Path) -> None:
        self.path = path
        self._lock = threading.Lock()
        self.count = 0

    def append(self, record: Mapping[str, Any]) -> None:
        line = json.dumps(dict(record), ensure_ascii=False)
        with self._lock:
            self.path.parent.mkdir(parents=True, exist_ok=True)
            with self.path.open("a", encoding="utf-8", newline="\n") as handle:
                handle.write(line + "\n")
            self.count += 1


@dataclass
class JobResult:
    job: Job
    status: str  # generated / skipped / failed / cancelled
    elapsed: float = 0.0
    meta: dict[str, Any] | None = None
    error: str | None = None


def _run_job(
    job: Job,
    *,
    model: str,
    size: str,
    force: bool,
    limiter: RateLimiter,
    manifest: ManifestWriter,
    stop_event: threading.Event,
    ref_images: list[Any],
    ref_sources: list[str],
) -> JobResult:
    """执行单个任务；任何异常都转成 failed 结果，保证批处理继续。"""
    started = time.perf_counter()
    if stop_event.is_set():
        return JobResult(job, "cancelled")

    if not force and output_is_valid(job):
        return JobResult(job, "skipped", time.perf_counter() - started)

    try:
        limiter.wait()
        if stop_event.is_set():
            return JobResult(job, "cancelled", time.perf_counter() - started)

        png = agnes_client.generate(
            job.prompt,
            size=size,
            model=model,
            images=list(ref_images) if ref_images else None,
            retries=RETRIES,
        )
        big, meta = normalize_artwork(png)
        small = big.resize(SMALL_SIZE, Image.Resampling.LANCZOS)
        _atomic_save_png(big, job.big_path)
        _atomic_save_png(small, job.small_path)
        elapsed = time.perf_counter() - started

        manifest.append(
            {
                "job_id": job.job_id,
                "slug": job.slug,
                "variant": job.variant,
                "display_name": (job.row.get("display_name") or "").strip(),
                "status": "generated",
                "big": _display_path(job.big_path),
                "small": _display_path(job.small_path),
                "model": model,
                "size": size,
                "prompt_version": believer_style.PROMPT_VERSION,
                "original_size": meta.get("original_size"),
                "content_bbox": meta.get("content_bbox"),
                "bars_trimmed": bool(meta.get("bars_trimmed")),
                "trim_px": meta.get("trim_px"),
                "normalized_size": meta.get("normalized_size"),
                "elapsed_s": round(elapsed, 2),
                "finished_at": _now_iso(),
                "refs": ref_sources,
                "prompt": job.prompt,
            }
        )
        return JobResult(job, "generated", elapsed, meta)
    except KeyboardInterrupt:
        raise
    except Exception as exc:  # 单张失败不影响整批
        elapsed = time.perf_counter() - started
        error = str(exc) if isinstance(exc, agnes_client.AgnesError) else f"{type(exc).__name__}: {exc}"
        try:
            manifest.append(
                {
                    "job_id": job.job_id,
                    "slug": job.slug,
                    "variant": job.variant,
                    "status": "failed",
                    "error": error,
                    "model": model,
                    "size": size,
                    "elapsed_s": round(elapsed, 2),
                    "finished_at": _now_iso(),
                }
            )
        except Exception:
            pass
        return JobResult(job, "failed", elapsed, None, error)


def _now_iso() -> str:
    return datetime.now().astimezone().isoformat(timespec="seconds")


def _display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


# ---------------------------------------------------------------------------
# 参考图（img2img）预处理
# ---------------------------------------------------------------------------


def plan_references(refs: Iterable[str]) -> list[dict[str, Any]]:
    """只做检查与计划（dry-run 也调用）；不写文件。"""
    notes: list[dict[str, Any]] = []
    for ref in refs:
        source = str(ref).strip()
        if not source:
            continue
        if source.startswith(("http://", "https://", "data:")):
            notes.append({"kind": "remote", "source": source, "action": "remote"})
            continue
        path = Path(source).expanduser()
        if not path.is_file():
            raise InputError(f"参考图不存在: {path}")
        try:
            with Image.open(path) as image:
                size = [image.width, image.height]
        except Exception as exc:
            raise InputError(f"参考图无法读取: {path}（{exc}）") from exc
        notes.append(
            {
                "kind": "local",
                "source": str(path),
                "path": path,
                "size": size,
                "action": "resize" if max(size) > REF_MAX_EDGE else "as-is",
            }
        )
    return notes


def prepare_references(notes: list[dict[str, Any]], temp_dir: Path) -> list[Any]:
    """把过大的本地参考图缩到长边 <= 768 并重编码 PNG，降低 413 风险。"""
    prepared: list[Any] = []
    for index, note in enumerate(notes, 1):
        if note["kind"] == "remote":
            prepared.append(note["source"])
            continue
        path = note["path"]
        if note["action"] == "as-is":
            prepared.append(path)
            continue
        with Image.open(path) as image:
            rgb = image.convert("RGB")
        rgb.thumbnail((REF_MAX_EDGE, REF_MAX_EDGE), Image.Resampling.LANCZOS)
        target = temp_dir / f"ref_{index:02d}.png"
        rgb.save(target, format="PNG", optimize=True)
        prepared.append(target)
    return prepared


def _print_reference_notes(notes: list[dict[str, Any]]) -> None:
    if not notes:
        print("[card_art] 参考图: 无（纯文生图）")
        return
    print(f"[card_art] 参考图: {len(notes)} 张（img2img）")
    for note in notes:
        if note["kind"] == "remote":
            preview = note["source"]
            if len(preview) > 90:
                preview = preview[:87] + "..."
            print(f"  - 远程图片，直接传给接口: {preview}")
        else:
            width, height = note["size"]
            if note["action"] == "as-is":
                print(f"  - {note['source']}（{width}x{height}）长边 <= {REF_MAX_EDGE}px，原样上传")
            else:
                print(
                    f"  - {note['source']}（{width}x{height}）上传前缩放为长边 <= {REF_MAX_EDGE}px 的 PNG"
                )


# ---------------------------------------------------------------------------
# 接触表
# ---------------------------------------------------------------------------


def _load_font(size: int) -> ImageFont.ImageFont:
    try:
        return ImageFont.load_default(size=size)
    except TypeError:  # 老版本 Pillow 不支持 size 参数
        return ImageFont.load_default()


def _fit_text(draw: ImageDraw.ImageDraw, text: str, font: ImageFont.ImageFont, max_width: int) -> str:
    if draw.textlength(text, font=font) <= max_width:
        return text
    while text and draw.textlength(text + "...", font=font) > max_width:
        text = text[:-1]
    return text + "..."


def _fit_font(draw: ImageDraw.ImageDraw, text: str, max_width: int, start: int = 24, minimum: int = 12) -> ImageFont.ImageFont:
    """从 start 逐级缩小字号，直到标题能放进画布宽度。"""
    size = start
    while size > minimum:
        font = _load_font(size)
        if draw.textlength(text, font=font) <= max_width:
            return font
        size -= 1
    return _load_font(minimum)


def write_contact_sheet(jobs: list[Job], path: Path, mode: str) -> int:
    """把已生成的小图画成接触表（PNG），返回图数量。"""
    entries = [job for job in jobs if job.small_path.is_file()]
    if not entries:
        print("[card_art] 接触表跳过：没有可用的小图")
        return 0

    columns = min(8, len(entries))
    rows = (len(entries) + columns - 1) // columns
    cell_w, cell_h = SMALL_SIZE
    padding = 14
    label_h = 20
    header_h = 46

    width = padding + columns * (cell_w + padding)
    height = header_h + rows * (cell_h + label_h) + (rows - 1) * padding + padding
    canvas = Image.new("RGB", (width, height), (14, 14, 18))
    draw = ImageDraw.Draw(canvas)

    title = believer_style.build_sheet_title(len(entries), mode)
    title_font = _fit_font(draw, title, width - 2 * padding)
    draw.text((padding, 12), title, font=title_font, fill=(235, 225, 200))

    label_font = _load_font(13)
    colors = {"normal": (205, 195, 170), "miracle": (230, 200, 130), "enhanced": (255, 225, 150)}
    for index, job in enumerate(entries):
        row, column = divmod(index, columns)
        x = padding + column * (cell_w + padding)
        y = header_h + row * (cell_h + label_h + padding)
        with Image.open(job.small_path) as small:
            canvas.paste(small.convert("RGB"), (x, y))
        draw.rectangle([x - 1, y - 1, x + cell_w, y + cell_h], outline=(120, 100, 60), width=1)
        label = _fit_text(draw, job.slug, label_font, cell_w - 8)
        text_width = draw.textlength(label, font=label_font)
        draw.text(
            (x + (cell_w - text_width) / 2, y + cell_h + 3),
            label,
            font=label_font,
            fill=colors.get(job.variant, (220, 220, 220)),
        )

    path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(path, format="PNG", optimize=True)
    return len(entries)


# ---------------------------------------------------------------------------
# 批处理执行
# ---------------------------------------------------------------------------


def _describe_trim(meta: Mapping[str, Any]) -> str:
    """把留白裁边信息压成一行。"""
    if not meta.get("bars_trimmed"):
        return "无留白"
    left, top, right, bottom = meta.get("trim_px") or [0, 0, 0, 0]
    return f"裁边 左{left}/上{top}/右{right}/下{bottom}"


def run_batch(
    jobs: list[Job],
    *,
    model: str,
    size: str,
    force: bool,
    concurrency: int,
    limiter: RateLimiter,
    manifest: ManifestWriter,
    ref_images: list[Any],
    ref_sources: list[str],
) -> tuple[Counter[str], list[JobResult], bool, float]:
    """并发跑完所有任务；返回 (统计, 失败列表, 是否中断, 总耗时)。"""
    stats: Counter[str] = Counter()
    failures: list[JobResult] = []
    stop_event = threading.Event()
    print_lock = threading.Lock()
    total = len(jobs)
    started = time.perf_counter()
    seen = 0
    interrupted = False

    def handle(result: JobResult) -> None:
        nonlocal seen
        stats[result.status] += 1
        seen += 1
        prefix = f"[{seen:02d}/{total:02d}]"
        if result.status == "generated":
            meta = result.meta or {}
            original = meta.get("original_size") or ["?", "?"]
            line = (
                f"{prefix} 生成 {result.job.job_id}（{result.job.slug}）"
                f" · {result.elapsed:.1f}s · 响应 {original[0]}x{original[1]}"
                f" · {_describe_trim(meta)} · 输出 1000x760 + 250x190"
            )
        elif result.status == "skipped":
            line = f"{prefix} 跳过 {result.job.job_id}（{result.job.slug}）· 已存在且尺寸正确（--force 可重生成）"
        elif result.status == "cancelled":
            line = f"{prefix} 取消 {result.job.job_id}（{result.job.slug}）· 批处理已中断"
        else:
            line = f"{prefix} 失败 {result.job.job_id}（{result.job.slug}）· {result.elapsed:.1f}s · {result.error}"
            failures.append(result)
        with print_lock:
            print(line)

    def collect(future: Any, job: Job) -> None:
        try:
            handle(future.result())
        except Exception as exc:  # 理论上 worker 不会抛，兜底
            handle(JobResult(job, "failed", 0.0, None, f"{type(exc).__name__}: {exc}"))

    with ThreadPoolExecutor(max_workers=concurrency, thread_name_prefix="cardart") as pool:
        futures = {
            pool.submit(
                _run_job,
                job,
                model=model,
                size=size,
                force=force,
                limiter=limiter,
                manifest=manifest,
                stop_event=stop_event,
                ref_images=ref_images,
                ref_sources=ref_sources,
            ): job
            for job in jobs
        }
        handled: set[Any] = set()
        try:
            for future in as_completed(futures):
                handled.add(future)
                collect(future, futures[future])
        except KeyboardInterrupt:
            interrupted = True
            stop_event.set()
            print("\n[card_art] 收到 Ctrl-C：停止派发新任务，等待进行中的请求结束（不会写半张图）")
            for future in futures:
                future.cancel()
        finally:
            # 中断后仍在进行的任务也会正常落盘，这里补收结果
            for future, job in futures.items():
                if future in handled:
                    continue
                if future.cancelled():
                    handle(JobResult(job, "cancelled", 0.0))
                elif future.done():
                    collect(future, job)

    return stats, failures, interrupted, time.perf_counter() - started


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------


def _positive_int(value: str) -> int:
    try:
        number = int(value)
    except ValueError as exc:
        raise argparse.ArgumentTypeError(f"需要正整数，收到 {value!r}") from exc
    if number < 1:
        raise argparse.ArgumentTypeError(f"需要正整数，收到 {value!r}")
    return number


def _default_rpm() -> int:
    raw = (os.environ.get("AGNES_RPM") or "").strip()
    if raw:
        try:
            value = int(raw)
        except ValueError:
            value = 0
        if value > 0:
            return value
    return DEFAULT_RPM


def build_cli() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="card_art.py",
        description="Believer 卡图批处理生成器：按 M10.2.1 规格表批量调用 Agnes 生成 64 张卡图。",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=EXAMPLES,
    )
    parser.add_argument(
        "--all",
        action="store_true",
        help=f"生成全部任务（默认行为；当前规格为 {EXPECTED_JOB_COUNT} 个）",
    )
    parser.add_argument(
        "--only",
        action="append",
        default=[],
        metavar="ID",
        help="只生成指定卡（可重复、可逗号分隔）：card_id 或 slug，如 BELIEVER-FOCUS,focus",
    )
    parser.add_argument("--limit", type=_positive_int, default=None, metavar="N", help="筛选后最多生成前 N 个任务")
    parser.add_argument("--staging", action="store_true", help="输出到暂存目录 Tools/AgnesImage/out/card_art（默认）")
    parser.add_argument(
        "--install",
        action="store_true",
        help="输出到游戏目录 Believer/images/card_portraits/{big}（会覆盖同名 PNG）",
    )
    parser.add_argument("--dry-run", action="store_true", help="只列出任务/提示词/输出路径，不调用 API、不写文件")
    parser.add_argument("--force", action="store_true", help="忽略已存在的图片，强制重新生成")
    parser.add_argument(
        "--concurrency",
        type=_positive_int,
        default=DEFAULT_CONCURRENCY,
        metavar="N",
        help=f"并发任务数（默认 {DEFAULT_CONCURRENCY}，建议不超过 2 以免触发队列满 503）",
    )
    parser.add_argument(
        "--rpm",
        type=_positive_int,
        default=_default_rpm(),
        metavar="N",
        help=f"每分钟请求数上限（默认读取 AGNES_RPM，否则 {DEFAULT_RPM}）",
    )
    parser.add_argument(
        "--manifest",
        default=None,
        metavar="JSONL",
        help="manifest 路径（默认 Tools/AgnesImage/out/manifest.jsonl）",
    )
    parser.add_argument(
        "--sheet",
        nargs="?",
        const=str(DEFAULT_SHEET),
        default=None,
        metavar="PNG",
        help="生成接触表 PNG（默认 Tools/AgnesImage/out/card_art/contact_sheet.png）",
    )
    parser.add_argument(
        "--ref",
        action="append",
        default=[],
        metavar="IMAGE",
        help=f"图生图参考图（可重复；长边 > {REF_MAX_EDGE}px 会自动缩放并重编码 PNG）",
    )
    parser.add_argument("--model", default=None, help="Agnes 模型名（默认读取 AGNES_MODEL 或 agnes-image-2.0-flash）")
    parser.add_argument(
        "--size",
        default=DEFAULT_REQUEST_SIZE,
        help=f"请求尺寸 WxH（默认 {DEFAULT_REQUEST_SIZE}；返回尺寸可能不同，最终统一裁切到 1000x760）",
    )
    return parser


def _validate_args(args: argparse.Namespace) -> None:
    if args.staging and args.install:
        raise InputError("--staging 与 --install 只能二选一")
    if not re.fullmatch(r"[1-9]\d*[xX][1-9]\d*", str(args.size).strip()):
        raise InputError(f"--size 格式应为 WxH（如 1024x768），收到: {args.size!r}")


def _distribution(jobs: Iterable[Job]) -> str:
    counts = Counter(job.variant for job in jobs)
    return (
        f"普通 {counts.get('normal', 0)} / 神迹 {counts.get('miracle', 0)}"
        f" / 强化 {counts.get('enhanced', 0)}"
    )


def _print_job_dry(job: Job, index: int, total: int) -> None:
    label = VARIANT_LABEL_ZH.get(job.variant, job.variant)
    print(f"[{index:02d}/{total:02d}] {label} {job.job_id}  slug={job.slug}")
    print(f"        big   : {_display_path(job.big_path)}")
    print(f"        small : {_display_path(job.small_path)}")
    print(
        textwrap.fill(
            "prompt: " + job.prompt,
            width=118,
            initial_indent="        ",
            subsequent_indent="        ",
            break_long_words=False,
            break_on_hyphens=False,
        )
    )


def _print_header(
    *,
    install: bool,
    root: Path,
    jobs: list[Job],
    model: str,
    size: str,
    concurrency: int,
    rpm: int,
    manifest_path: Path,
) -> None:
    print("[card_art] 规格表: docs/M10.2.1-CardArt-Spec.csv")
    print(f"[card_art] 任务: {len(jobs)} 个（{_distribution(jobs)}）")
    mode = "安装目录" if install else "暂存目录"
    print(f"[card_art] 输出: {mode} {_display_path(root)}（big 1000x760 · small 250x190 · RGB PNG）")
    print(f"[card_art] 模型: {model} · 请求尺寸 {size} · 重试 {RETRIES} 次 · 并发 {concurrency} · 限速 {rpm} RPM")
    print(f"[card_art] manifest: {_display_path(manifest_path)}")
    if install:
        print("[card_art] 注意: --install 会直接覆盖 Believer/images/card_portraits 下同名 PNG")
    else:
        print("[card_art] 提示: 已存在且尺寸正确的图片会跳过；--force 可强制重生成")


def _run_cli(args: argparse.Namespace) -> int:
    _validate_args(args)

    rows = load_spec_rows(SPEC_CSV)
    install = bool(args.install)
    root = INSTALL_DIR if install else STAGING_DIR
    jobs, warnings = build_jobs(rows, root=root, install=install)
    for warning in warnings:
        print(f"[card_art] 警告: {warning}")

    if len(rows) != EXPECTED_JOB_COUNT:
        print(f"[card_art] 警告: 规格表有 {len(rows)} 行，预期 {EXPECTED_JOB_COUNT} 行")
    if len(jobs) != EXPECTED_JOB_COUNT:
        print(f"[card_art] 警告: 展开出 {len(jobs)} 个任务，预期 {EXPECTED_JOB_COUNT} 个")

    # --only 过滤
    selectors = _parse_selectors(args.only)
    if selectors:
        unmatched = [
            selector for selector in selectors if not any(_job_matches(job, [selector]) for job in jobs)
        ]
        if unmatched:
            print("[card_art] 警告: 以下 --only 选择器没有匹配到任务:", ", ".join(unmatched))
        jobs = [job for job in jobs if _job_matches(job, selectors)]
        if not jobs:
            raise InputError(f"--only 没有匹配到任何任务: {', '.join(selectors)}")

    if args.limit is not None:
        jobs = jobs[: args.limit]

    model = agnes_client.resolve_model(args.model)
    manifest_path = Path(args.manifest) if args.manifest else DEFAULT_MANIFEST

    ref_notes = plan_references(args.ref)

    _print_header(
        install=install,
        root=root,
        jobs=jobs,
        model=model,
        size=args.size,
        concurrency=args.concurrency,
        rpm=args.rpm,
        manifest_path=manifest_path,
    )
    _print_reference_notes(ref_notes)

    # ---- dry-run：只列任务，绝不调用 API ----
    if args.dry_run:
        print("[card_art] dry-run：不调用 API，仅列出任务、提示词与输出路径")
        print()
        for index, job in enumerate(jobs, 1):
            _print_job_dry(job, index, len(jobs))
        print()
        print(f"[card_art] dry-run 汇总: {len(jobs)} 个任务（{_distribution(jobs)}），未调用 API")
        return 0

    # ---- 真实生成 ----
    try:
        agnes_client.load_api_key()  # 只校验配置，密钥不打印、不外泄
    except agnes_client.AgnesConfigError as exc:
        raise InputError(f"Agnes 配置错误: {exc}") from exc

    temp_dir: Path | None = None
    ref_images: list[Any] = []
    ref_sources = [str(ref) for ref in args.ref]
    if ref_notes:
        temp_dir = Path(tempfile.mkdtemp(prefix="believer_refs_"))
        ref_images = prepare_references(ref_notes, temp_dir)

    manifest = ManifestWriter(manifest_path)
    limiter = RateLimiter(args.rpm)

    try:
        stats, failures, interrupted, elapsed = run_batch(
            jobs,
            model=model,
            size=args.size,
            force=bool(args.force),
            concurrency=args.concurrency,
            limiter=limiter,
            manifest=manifest,
            ref_images=ref_images,
            ref_sources=ref_sources,
        )
    finally:
        if temp_dir is not None:
            shutil.rmtree(temp_dir, ignore_errors=True)

    print(
        f"[card_art] 完成: 生成 {stats.get('generated', 0)} / 跳过 {stats.get('skipped', 0)}"
        f" / 失败 {stats.get('failed', 0)} / 取消 {stats.get('cancelled', 0)} · 总耗时 {elapsed:.1f}s"
    )
    if stats.get("skipped"):
        print("[card_art] 说明: 跳过任务不写 manifest（只有实际生成/失败会记录）")
    if failures:
        print("[card_art] 失败明细（可单独重跑，例如 --only <slug> --force）:")
        for result in failures[:20]:
            print(f"  - {result.job.job_id}（{result.job.slug}）: {result.error}")
        if len(failures) > 20:
            print(f"  ... 其余 {len(failures) - 20} 条见 manifest")

    # 接触表
    if args.sheet:
        sheet_path = Path(args.sheet)
        mode = "install" if install else "staging"
        count = write_contact_sheet(jobs, sheet_path, mode)
        if count:
            print(f"[card_art] 接触表: {_display_path(sheet_path)}（{count} 张）")

    if interrupted:
        return 130
    return 1 if failures else 0


def main(argv: list[str] | None = None) -> int:
    args = build_cli().parse_args(argv)
    try:
        return _run_cli(args)
    except InputError as exc:
        print(f"[card_art] 输入错误: {exc}", file=sys.stderr)
        return 2
    except KeyboardInterrupt:
        print("\n[card_art] 已中断。已完成的图片与 manifest 记录均已落盘。", file=sys.stderr)
        return 130


if __name__ == "__main__":
    raise SystemExit(main())

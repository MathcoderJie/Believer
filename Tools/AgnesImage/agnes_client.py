"""Agnes AI 图像生成客户端（文生图 / 图生图 / 多图合成）。

仅依赖 Python 标准库，可被 ``card_art.py`` 等批处理脚本导入。

API 文档: https://agnes-ai.cn/zh-Hans/docs/agnes-image-20-flash
Endpoint: ``POST https://apihub.agnes-ai.com/v1/images/generations``（国际站）
          ``POST https://api.agnes-ai.cn/v1/images/generations``（中国站，兼容回退）

密钥读取顺序:
    1. 显式参数 ``api_key``
    2. 环境变量 ``AGNES_API_KEY``
    3. ``Tools/AgnesImage/.env``
    4. ``<repo>/.env``

密钥和完整请求体不会写入日志或异常信息。

用法::

    python agnes_client.py "a hooded pilgrim in ivory robes" -o out.png
    python agnes_client.py "make it golden" -o out.png --image ref.png

    from agnes_client import generate, generate_to_file
    png = generate("prompt", size="1024x768")
"""

from __future__ import annotations

import argparse
import base64
import json
import mimetypes
import os
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]  # Tools/AgnesImage -> repo root

DEFAULT_ENDPOINT = "https://apihub.agnes-ai.com/v1/images/generations"
# 同一把密钥只在一个站点有效；401/404 时自动换站重试一次。
KNOWN_ENDPOINTS = (
    "https://apihub.agnes-ai.com/v1/images/generations",
    "https://api.agnes-ai.cn/v1/images/generations",
)
DEFAULT_MODEL = "agnes-image-2.0-flash"
SUPPORTED_MODELS = (
    "agnes-image-2.0-flash",
    "agnes-image-2.1-flash",
    "agnes-image-2.5-flash",
)

# 文档建议 60s-360s；一次生成通常 10-60s。
DEFAULT_TIMEOUT = 240
RETRYABLE_STATUS = {408, 409, 425, 429, 500, 502, 503, 504}


class AgnesError(RuntimeError):
    """Agnes API 调用失败。``status`` 为 HTTP 状态码（网络错误时为 None）。"""

    def __init__(self, message: str, status: int | None = None):
        super().__init__(message)
        self.status = status


class AgnesConfigError(AgnesError):
    """缺少密钥或配置无效。"""


def _parse_dotenv(path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    if not path.is_file():
        return values
    for raw in path.read_text(encoding="utf-8", errors="replace").splitlines():
        line = raw.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, _, value = line.partition("=")
        values[key.strip()] = value.strip().strip('"').strip("'")
    return values


def load_api_key(api_key: str | None = None) -> str:
    """按优先级解析 API key；找不到时抛 ``AgnesConfigError``。"""
    if api_key:
        return api_key

    env_value = os.environ.get("AGNES_API_KEY")
    if env_value:
        return env_value

    for candidate in (HERE / ".env", ROOT / ".env"):
        value = _parse_dotenv(candidate).get("AGNES_API_KEY")
        if value:
            return value

    raise AgnesConfigError(
        "未找到 AGNES_API_KEY。请复制 Tools/AgnesImage/.env.example 为 .env 并填入密钥，"
        "或设置环境变量 AGNES_API_KEY。"
    )


def resolve_model(model: str | None = None) -> str:
    if model:
        return model
    env_value = os.environ.get("AGNES_MODEL")
    if env_value:
        return env_value
    for candidate in (HERE / ".env", ROOT / ".env"):
        value = _parse_dotenv(candidate).get("AGNES_MODEL")
        if value:
            return value
    return DEFAULT_MODEL


def resolve_endpoint(endpoint: str | None = None) -> str:
    """显式参数 > 环境变量 > .env；都没有时返回空串，由 ``_endpoint_candidates`` 决定。"""
    if endpoint:
        return endpoint
    env_value = os.environ.get("AGNES_API_ENDPOINT")
    if env_value:
        return env_value
    for candidate in (HERE / ".env", ROOT / ".env"):
        value = _parse_dotenv(candidate).get("AGNES_API_ENDPOINT")
        if value:
            return value
    return ""


_WORKING_ENDPOINT: list[str | None] = [None]


def _endpoint_candidates(explicit: str | None) -> tuple[str, ...]:
    configured = resolve_endpoint(explicit)
    if configured:
        return (configured,)
    cached = _WORKING_ENDPOINT[0]
    ordered = ([cached] if cached else []) + [e for e in KNOWN_ENDPOINTS if e != cached]
    return tuple(ordered)


def file_to_data_uri(path: str | Path) -> str:
    """把本地图片转成 Data URI（图生图输入）。"""
    file_path = Path(path)
    if not file_path.is_file():
        raise AgnesConfigError(f"参考图不存在: {file_path}")
    mime = mimetypes.guess_type(file_path.name)[0] or "image/png"
    encoded = base64.b64encode(file_path.read_bytes()).decode("ascii")
    return f"data:{mime};base64,{encoded}"


def _is_remote_image(value: str) -> bool:
    return value.startswith(("http://", "https://", "data:"))


def _http_post_json(url: str, payload: dict, api_key: str, timeout: float) -> dict:
    body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
    request = urllib.request.Request(
        url,
        data=body,
        method="POST",
        headers={
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json",
            "Accept": "application/json",
            "User-Agent": "Believer-AgnesImage/1.0",
        },
    )
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            raw = response.read()
    except urllib.error.HTTPError as exc:
        detail = ""
        try:
            detail = exc.read().decode("utf-8", "replace")
        except Exception:  # pragma: no cover - 仅用于错误提示
            pass
        message = _explain_http_error(exc.code, detail)
        raise AgnesError(message, status=exc.code) from None
    except urllib.error.URLError as exc:
        raise AgnesError(f"网络错误: {exc.reason}") from None
    except TimeoutError:
        raise AgnesError("请求超时") from None

    try:
        return json.loads(raw.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError):
        raise AgnesError("响应不是合法 JSON（前 200 字节）：" + raw[:200].decode("utf-8", "replace")) from None


def _explain_http_error(status: int, detail: str) -> str:
    hint = {
        400: "请求参数不合法（检查 size/模型名，response_format 必须放在 extra_body 内）",
        401: "API key 无效或已过期",
        403: "无权访问该模型或额度不足",
        404: "接口地址或模型不存在",
        429: "触发限流，请降低并发或稍后重试",
    }.get(status, "服务端错误")
    detail = detail.strip()
    if len(detail) > 500:
        detail = detail[:500] + "..."
    return f"Agnes API HTTP {status}: {hint}" + (f" | {detail}" if detail else "")


def _read_image_bytes(url: str, timeout: float) -> bytes:
    try:
        with urllib.request.urlopen(url, timeout=timeout) as response:
            return response.read()
    except urllib.error.URLError as exc:
        raise AgnesError(f"下载生成结果失败: {exc.reason}") from None


def generate_result(
    prompt: str,
    *,
    size: str = "1024x768",
    model: str | None = None,
    images: list[str | Path] | None = None,
    return_base64: bool = False,
    timeout: float = DEFAULT_TIMEOUT,
    retries: int = 3,
    api_key: str | None = None,
    endpoint: str | None = None,
    extra_body: dict | None = None,
) -> dict:
    """调用一次生图接口，返回 ``{"png": bytes, "url": str|None, "revised_prompt": str|None}``。

    ``images`` 支持本地路径、公网 URL 和 Data URI；传了就是图生图/多图合成。
    """
    if not prompt or not prompt.strip():
        raise AgnesConfigError("prompt 不能为空")
    if not _valid_size(size):
        raise AgnesConfigError(f"size 格式应为 WxH，例如 1024x768，收到: {size!r}")

    resolved_key = load_api_key(api_key)
    resolved_model = resolve_model(model)
    candidates = _endpoint_candidates(endpoint)

    payload: dict = {
        "model": resolved_model,
        "prompt": prompt,
        "size": size,
        "extra_body": dict(extra_body or {}),
    }

    if images:
        normalized = [
            image if _is_remote_image(str(image)) else file_to_data_uri(image)
            for image in images
        ]
        payload["extra_body"]["image"] = normalized

    if return_base64:
        payload["return_base64"] = True
        payload["extra_body"]["response_format"] = "b64_json"
    elif "response_format" not in payload["extra_body"]:
        payload["extra_body"]["response_format"] = "url"

    last_error: AgnesError | None = None
    for position, candidate in enumerate(candidates):
        try:
            return _generate_once(candidate, payload, resolved_key, timeout, retries)
        except AgnesError as exc:
            last_error = exc
            if position + 1 < len(candidates) and exc.status in (401, 404):
                print(
                    f"[agnes] {candidate} 拒绝该密钥（HTTP {exc.status}），尝试下一站点",
                    file=sys.stderr,
                )
                continue
            raise
    assert last_error is not None
    raise last_error


def _generate_once(
    endpoint: str, payload: dict, api_key: str, timeout: float, retries: int
) -> dict:
    attempt = 0
    while True:
        attempt += 1
        try:
            response = _http_post_json(endpoint, payload, api_key, timeout)
            _WORKING_ENDPOINT[0] = endpoint
            return _decode_image_response(response, timeout)
        except AgnesError as exc:
            if exc.status is not None and exc.status not in RETRYABLE_STATUS:
                raise
            if attempt > retries:
                raise
            delay = min(2 ** attempt, 30)
            print(
                f"[agnes] 第 {attempt} 次失败: {exc}；{delay}s 后重试",
                file=sys.stderr,
            )
            time.sleep(delay)


def _valid_size(size: str) -> bool:
    try:
        width, height = size.lower().split("x", 1)
        return int(width) > 0 and int(height) > 0
    except (ValueError, AttributeError):
        return False


def _decode_image_response(response: dict, timeout: float) -> dict:
    data = response.get("data")
    if not isinstance(data, list) or not data:
        raise AgnesError("响应缺少 data 数组: " + json.dumps(response, ensure_ascii=False)[:300])

    item = data[0] or {}
    b64 = item.get("b64_json")
    url = item.get("url")
    revised = item.get("revised_prompt")

    if b64:
        try:
            png = base64.b64decode(b64)
        except (ValueError, TypeError):
            raise AgnesError("b64_json 解码失败") from None
    elif url:
        png = _read_image_bytes(url, timeout)
    else:
        raise AgnesError("响应中既没有 b64_json 也没有 url")

    if not png:
        raise AgnesError("生成结果为空")
    return {"png": png, "url": url, "revised_prompt": revised}


def generate(prompt: str, **kwargs) -> bytes:
    """文生图/图生图的简写，直接返回 PNG 字节。"""
    return generate_result(prompt, **kwargs)["png"]


def generate_to_file(prompt: str, output: str | Path, **kwargs) -> Path:
    """生成并写入文件（自动创建父目录）。"""
    output_path = Path(output)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_bytes(generate(prompt, **kwargs))
    return output_path


def build_cli() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Agnes AI 生图客户端：文生图 / 图生图 / 多图合成。",
    )
    parser.add_argument("prompt", help="提示词")
    parser.add_argument("-o", "--out", default="agnes_out.png", help="输出 PNG 路径")
    parser.add_argument("--size", default="1024x768", help="输出尺寸，默认 1024x768")
    parser.add_argument("--model", default=None, help="模型名，默认读取 AGNES_MODEL 或 agnes-image-2.0-flash")
    parser.add_argument("--image", action="append", default=[], help="参考图路径/URL，可重复")
    parser.add_argument("--base64", action="store_true", help="要求接口返回 b64_json（默认拿 URL 再下载）")
    parser.add_argument("--timeout", type=float, default=DEFAULT_TIMEOUT, help="单次请求超时秒数")
    parser.add_argument("--retries", type=int, default=3, help="可重试错误的最大重试次数")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_cli().parse_args(argv)
    try:
        start = time.time()
        result = generate_result(
            args.prompt,
            size=args.size,
            model=args.model,
            images=args.image or None,
            return_base64=args.base64,
            timeout=args.timeout,
            retries=args.retries,
        )
        output = Path(args.out)
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_bytes(result["png"])
        elapsed = time.time() - start
        print(f"[agnes] 已保存 {output} ({len(result['png']) / 1024:.0f} KB, {elapsed:.1f}s)")
        if result.get("revised_prompt"):
            print(f"[agnes] revised_prompt: {result['revised_prompt']}")
        return 0
    except AgnesConfigError as exc:
        print(f"[agnes] 配置错误: {exc}", file=sys.stderr)
        return 2
    except AgnesError as exc:
        print(f"[agnes] 失败: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

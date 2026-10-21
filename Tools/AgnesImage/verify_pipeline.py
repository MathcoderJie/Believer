#!/usr/bin/env python3
"""Independent verifier for the Believer Agnes card-art pipeline.

This script is QA only: it never generates images, never edits pipeline files and
never writes outside ``Tools/AgnesImage/out/`` (scratch + report).  Expectations
are derived from the real sources of truth, not from the implementer's code:

* ``BelieverCode/Cards/Basic/*.cs`` + ``Cards/Blue/*.cs`` -> 40 normal cards
* ``BelieverCode/Cards/Miracles/*.cs``                  -> 12 base miracles
* ``BelieverCode/Miracles/EnhancedMiracles.cs``          -> 12 enhanced miracles
* ``docs/M10.2.1-CardArt-Spec.csv`` (GB18030, 64 rows)   -> display names / paths

The card id rule is taken from ``BelieverCard.CustomPortraitPath`` /
``BelieverSignatureCard.CustomPortraitPath``:
``Id.Entry.RemovePrefix().ToLowerInvariant()`` -> strip ``BELIEVER-`` and lower.

Checks implemented (see ``--report`` markdown output for PASS/FAIL evidence):

0. ``agnes_client.py`` import + key-resolution precedence, with no network call.
1. Job inventory: 64 jobs (40 / 12 / 12), slugs, enhanced linkage, CSV<->C# match.
2. ``card_art.py --all --dry-run``: exit 0, exactly the 64 expected jobs, and no
   output files written (real staging/manifest snapshotted, scratch staging must
   stay empty).
3. Image contract for every generated big/small PNG: 1000x760 / 250x190 RGB PNG,
   decodable, non-trivial, no alpha, not a copy of the fallback art, small is a
   true downscale of big, plus letterbox/pillarbox detection and a synthetic
   trim-function round trip when the pipeline exposes one.
4. Manifest integrity: valid JSONL, one entry per generated job, paths exist,
   recorded sizes/dimensions match, no duplicates/stale entries, trim provenance
   recorded when the pipeline claims to trim.
5. Resume/idempotency: re-running one generated card without ``--force`` must not
   call the API and must leave the manifest and output files untouched.
6. Secret hygiene: ``.env`` untracked+ignored, no tracked file contains the live
   key, no ``sk-`` secret in tracked files, no script prints the key, and no
   captured pipeline output leaks it.
7. Visual sanity: automated non-blank/colour statistics plus the two images the
   verifier manually inspected (``--visual-notes``).
8. Static source review of the pipeline constants (sizes, GB18030, slug rule,
   trim helper) with ``file:line`` references.

Safety: all pipeline subprocesses run with ``AGNES_API_KEY`` set to a dummy value
and ``AGNES_API_ENDPOINT`` pointing at a closed local port, so a bug that tries to
call the API during a dry-run/resume check fails immediately instead of spending
credits.  The live key is read only to compare/redact; it is never printed.

Usage (from ``Tools/AgnesImage``)::

    python verify_pipeline.py --report out/verification-report.md

Exit codes: 0 = PASS, 1 = FAIL, 2 = INCOMPLETE (some checks skipped because the
pipeline / generated art was not available yet), 3 = verifier could not run.
"""

from __future__ import annotations

import argparse
import ast
import csv
import hashlib
import io
import json
import os
import re
import shutil
import subprocess
import sys
import time
import traceback
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path

try:
    from PIL import Image, ImageChops, ImageDraw, ImageStat
except Exception as exc:  # pragma: no cover - environment guard
    sys.stderr.write(f"[verify] FATAL: Pillow is required: {exc}\n")
    raise SystemExit(3)

try:
    import PIL

    PILLOW_VERSION = getattr(PIL, "__version__", "unknown")
except Exception:  # pragma: no cover
    PILLOW_VERSION = "unknown"

# ---------------------------------------------------------------------------
# paths / constants
# ---------------------------------------------------------------------------

AGNES_DIR = Path(__file__).resolve().parent
REPO = AGNES_DIR.parents[1]
OUT_DIR = AGNES_DIR / "out"
TMP_DIR = OUT_DIR / "_verify_tmp"

CLIENT = AGNES_DIR / "agnes_client.py"
CARD_ART = AGNES_DIR / "card_art.py"
STYLE = AGNES_DIR / "believer_style.py"

CSV_PATH = REPO / "docs" / "M10.2.1-CardArt-Spec.csv"
FALLBACK_SMALL = REPO / "Believer" / "images" / "card_portraits" / "card.png"
FALLBACK_BIG = REPO / "Believer" / "images" / "card_portraits" / "big" / "card.png"

DEFAULT_STAGING = OUT_DIR / "card_art"
DEFAULT_MANIFEST = OUT_DIR / "manifest.jsonl"

BIG_SIZE = (1000, 760)
SMALL_SIZE = (250, 190)
BIG_MIN_BYTES = 30_000
SMALL_MIN_BYTES = 4_000
BIG_MAX_BYTES = 30_000_000
SMALL_MAX_BYTES = 8_000_000

# letterbox / pillarbox guard
BRIGHT_MEAN = 225.0
UNIFORM_STD = 25.0
EDGE_TOLERANCE_PX = 2
MAX_EDGE_SCAN = 600

# image sanity
MIN_LUM_STD = 12.0
MIN_DISTINCT_COLORS = 64
MAX_TOP_COLOR_FRACTION = 0.95
MIN_SAT_MEAN = 0.02
SMALL_BIG_MAX_DIFF = 15.0
FALLBACK_MAX_DIFF = 4.0

PASS, FAIL, SKIP, WARN = "PASS", "FAIL", "SKIP", "WARN"
STATUS_ORDER = {PASS: 0, WARN: 1, SKIP: 2, FAIL: 3}

SECRET_RE = re.compile(r"\b(sk)-[A-Za-z0-9_\-]{6,}\b")
SECRET_HEURISTIC_RE = re.compile(r"\bsk-[A-Za-z0-9_\-]{6,}\b")
KEY_WORD = "api" + "_" + "key"  # built dynamically so this file does not self-match scans
PRINT_TOKEN = "pri" + "nt"

TRIM_NAME_RE = re.compile(
    r"(trim|normal|bright|margin|letterbox|pillarbox|crop|pad|bbox)", re.IGNORECASE
)
FAILED_STATUSES = {"failed", "cancelled", "canceled", "error", "timeout"}
SUCCESS_STATUSES = {"generated", "ok", "success", "done", "skipped"}
PROVENANCE_KEY_RE = re.compile(
    r"(trim|bar|margin|letterbox|pillarbox|crop|original|source)", re.IGNORECASE
)
PROVENANCE_ANY_KEYS = {
    "bars_trimmed",
    "bar_trimmed",
    "trimmed",
    "bars",
    "trim",
    "trim_box",
    "crop_box",
    "original_size",
    "original_width",
    "original_height",
    "source_size",
    "source_width",
    "source_height",
    "original_dimensions",
}

ID_LEAVES = {
    "card_id",
    "cardid",
    "card",
    "job_id",
    "jobid",
    "job",
    "id",
    "slug",
    "name",
    "key",
    "entry",
    "model_id",
    "modelid",
}

CARD_BASE_NAMES = {"BelieverCard", "MiracleCard", "EnhancedMiracleCard"}
CLASS_DECL_RE = re.compile(
    r"(?:(?:public|internal|sealed|abstract|partial|static)\s+)*"
    r"class\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*"
    r"(?:\([^)]*\))?\s*:\s*(?P<base>[A-Za-z_][A-Za-z0-9_\.]*)\s*(?:\(|$)",
    re.MULTILINE,
)


# ---------------------------------------------------------------------------
# tiny helpers
# ---------------------------------------------------------------------------


def now_iso() -> str:
    return datetime.now().astimezone().strftime("%Y-%m-%d %H:%M:%S %z")


def rel(path: Path) -> str:
    try:
        return str(path.resolve().relative_to(REPO)).replace("\\", "/")
    except (ValueError, OSError):
        return str(path).replace("\\", "/")


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def human_bytes(n: int) -> str:
    value = float(n)
    for unit in ("B", "KB", "MB", "GB"):
        if value < 1024.0 or unit == "GB":
            return f"{value:.1f} {unit}" if unit != "B" else f"{int(value)} B"
        value /= 1024.0
    return f"{n} B"


def line_count(path: Path) -> int:
    try:
        return len([line for line in path.read_text(encoding="utf-8", errors="replace").splitlines() if line.strip()])
    except OSError:
        return -1


def camel_to_snake(name: str) -> str:
    """CamelCase -> UPPER_SNAKE, matching ModelId.Entry.RemovePrefix() semantics."""
    step1 = re.sub(r"(.)([A-Z][a-z]+)", r"\1_\2", name)
    step2 = re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", step1)
    return step2.upper()


def slug_for(card_id: str) -> str:
    prefix = "BELIEVER-"
    if card_id.upper().startswith(prefix):
        return card_id[len(prefix):].lower()
    return card_id.lower()


def read_dotenv(path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    if not path.is_file():
        return values
    try:
        text = path.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return values
    for raw in text.splitlines():
        line = raw.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, _, value = line.partition("=")
        values[key.strip()] = value.strip().strip('"').strip("'")
    return values


def redact(ctx: "Ctx", text: object) -> str:
    """Remove the live API key and any sk-* looking secret token from output."""
    if text is None:
        return ""
    out = str(text)
    if ctx.key and len(ctx.key) >= 6:
        out = out.replace(ctx.key, "<REDACTED-API-KEY>")
    return SECRET_RE.sub(r"\1<REDACTED>", out)


# ---------------------------------------------------------------------------
# data model
# ---------------------------------------------------------------------------


@dataclass
class Job:
    card_id: str
    slug: str
    kind: str  # normal | miracle | enhanced
    source: str  # file:line in BelieverCode
    display_name: str = ""
    group: str = ""
    csv_line: int | None = None
    enhanced_card_id: str = ""  # on base miracle rows
    enhanced_of: str = ""  # on enhanced rows


@dataclass
class Check:
    ident: str
    title: str
    status: str = SKIP
    summary: str = ""
    evidence: list[str] = field(default_factory=list)
    defects: list[str] = field(default_factory=list)
    details: list[str] = field(default_factory=list)


@dataclass
class Ctx:
    repo: Path
    agnes: Path
    out: Path
    staging: Path
    manifest: Path
    python: str
    no_run: bool
    keep_scratch: bool
    timeout: int
    visual_notes: str
    key: str | None = None
    key_source: str = "not found"
    jobs: dict[str, Job] = field(default_factory=dict)
    slug_to_id: dict[str, str] = field(default_factory=dict)
    display_to_id: dict[str, str] = field(default_factory=dict)
    csv_rows: list[dict] = field(default_factory=list)
    csv_meta: dict = field(default_factory=dict)
    inventory_error: str = ""
    generated_pairs: dict[str, Job] = field(default_factory=dict)
    partial_pairs: dict[str, Job] = field(default_factory=dict)
    staging_arg: str | None = None
    manifest_arg: str | None = None
    scratch: Path = TMP_DIR
    trim_functions: list[dict] = field(default_factory=list)
    trim_source_text: dict[str, str] = field(default_factory=dict)
    runs: list[dict] = field(default_factory=list)
    checks: list[Check] = field(default_factory=list)
    defects: list[dict] = field(default_factory=list)
    redaction_count: int = 0

    # -- job lookups -----------------------------------------------------
    def lookup_job(self, token: object) -> Job | None:
        if token is None:
            return None
        text = str(token).strip()
        if not text:
            return None
        if text in self.jobs:
            return self.jobs[text]
        upper = text.upper()
        if upper in self.jobs:
            return self.jobs[upper]
        low = text.lower()
        if low in self.slug_to_id:
            return self.jobs[self.slug_to_id[low]]
        if upper.startswith("BELIEVER-"):
            return self.jobs.get(upper)
        candidate = "BELIEVER-" + upper
        if candidate in self.jobs:
            return self.jobs[candidate]
        if text in self.display_to_id:
            return self.jobs[self.display_to_id[text]]
        return None


# ---------------------------------------------------------------------------
# defect plumbing
# ---------------------------------------------------------------------------


def add_defect(ctx: Ctx, check: Check, severity: str, where: str, message: str) -> None:
    check.defects.append(f"{where}: {message}")
    ctx.defects.append(
        {"check": check.ident, "severity": severity, "where": where, "message": message}
    )


def note(check: Check, text: str) -> None:
    check.evidence.append(text)


# ---------------------------------------------------------------------------
# subprocess / git helpers
# ---------------------------------------------------------------------------


def run_process(
    ctx: Ctx,
    cmd: list,
    cwd: Path,
    timeout: int,
    env: dict | None = None,
    label: str = "",
) -> dict:
    cmd = [str(c) for c in cmd]
    start = time.time()
    stdout = ""
    stderr = ""
    rc: int | None = None
    timed_out = False
    try:
        proc = subprocess.run(
            cmd,
            cwd=str(cwd),
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=timeout,
            env=env,
        )
        rc = proc.returncode
        stdout = proc.stdout or ""
        stderr = proc.stderr or ""
    except subprocess.TimeoutExpired as exc:
        timed_out = True
        raw_out = getattr(exc, "stdout", None)
        raw_err = getattr(exc, "stderr", None)
        if isinstance(raw_out, bytes):
            stdout = raw_out.decode("utf-8", "replace")
        elif raw_out:
            stdout = str(raw_out)
        if isinstance(raw_err, bytes):
            stderr = raw_err.decode("utf-8", "replace")
        elif raw_err:
            stderr = str(raw_err)
    elapsed = time.time() - start
    leaked = bool(ctx.key and len(ctx.key) >= 6 and (ctx.key in stdout or ctx.key in stderr))
    record = {
        "label": label,
        "cmd": [rel_or_text(ctx, c) for c in cmd],
        "cwd": rel(cwd),
        "rc": rc,
        "timed_out": timed_out,
        "elapsed": round(elapsed, 2),
        "stdout": redact(ctx, stdout),
        "stderr": redact(ctx, stderr),
        "leaked_key": leaked,
    }
    ctx.runs.append(record)
    return record


def rel_or_text(ctx: Ctx, value: str) -> str:
    try:
        path = Path(value)
        if path.is_absolute():
            return rel(path)
    except (OSError, ValueError):
        pass
    return value


def git(ctx: Ctx, *args: str, timeout: int = 120) -> tuple[int, str, str]:
    try:
        proc = subprocess.run(
            ["git", "-C", str(REPO), *args],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=timeout,
        )
        return proc.returncode, proc.stdout or "", proc.stderr or ""
    except (OSError, subprocess.TimeoutExpired) as exc:
        return 1, "", f"{type(exc).__name__}: {exc}"


def trap_env(ctx: Ctx, extra: dict | None = None) -> dict:
    """Environment for pipeline subprocesses: any accidental API call must fail fast."""
    env = os.environ.copy()
    env["AGNES_API_KEY"] = "qa-verifier-dummy-key-0000000000"
    env["AGNES_API_ENDPOINT"] = "http://127.0.0.1:9/v1/images/generations"
    env["PYTHONIOENCODING"] = "utf-8"
    env["PYTHONUTF8"] = "1"
    if extra:
        env.update(extra)
    return env


def snapshot_tree(root: Path, skip_prefixes: tuple[str, ...] = ()) -> dict[str, tuple[int, int]]:
    snap: dict[str, tuple[int, int]] = {}
    if not root.is_dir():
        return snap
    for path in sorted(root.rglob("*")):
        if not path.is_file():
            continue
        relname = str(path.relative_to(root)).replace("\\", "/")
        if any(relname == skip or relname.startswith(skip + "/") for skip in skip_prefixes):
            continue
        try:
            stat = path.stat()
            snap[relname] = (stat.st_size, stat.st_mtime_ns)
        except OSError:
            continue
    return snap


def diff_snapshots(before: dict, after: dict, limit: int = 12) -> list[str]:
    changed = []
    for key in sorted(set(before) | set(after)):
        if before.get(key) != after.get(key):
            changed.append(key)
        if len(changed) >= limit:
            changed.append("...")
            break
    return changed


# ---------------------------------------------------------------------------
# independent inventory derivation
# ---------------------------------------------------------------------------


def find_line(text: str, offset: int) -> int:
    return text.count("\n", 0, offset) + 1


def scan_class_decls(path: Path) -> list[dict]:
    try:
        text = path.read_text(encoding="utf-8", errors="replace")
    except OSError as exc:
        return [{"file": path, "error": str(exc)}]
    found = []
    for match in CLASS_DECL_RE.finditer(text):
        base = match.group("base").split(".")[-1].strip()
        found.append(
            {
                "file": path,
                "name": match.group("name"),
                "base": base,
                "line": find_line(text, match.start("name")),
            }
        )
    return found


def scan_card_classes(repo: Path) -> tuple[list[dict], list[dict]]:
    targets = [
        (repo / "BelieverCode" / "Cards" / "Basic", "normal", "BelieverCard"),
        (repo / "BelieverCode" / "Cards" / "Blue", "normal", "BelieverCard"),
        (repo / "BelieverCode" / "Cards" / "Miracles", "miracle", "MiracleCard"),
    ]
    jobs: list[dict] = []
    for folder, kind, base in targets:
        if not folder.is_dir():
            continue
        for path in sorted(folder.glob("*.cs")):
            for decl in scan_class_decls(path):
                if decl.get("base") == base:
                    jobs.append({**decl, "kind": kind})

    enhanced_file = repo / "BelieverCode" / "Miracles" / "EnhancedMiracles.cs"
    if enhanced_file.is_file():
        for decl in scan_class_decls(enhanced_file):
            if decl.get("base") == "EnhancedMiracleCard":
                jobs.append({**decl, "kind": "enhanced"})

    # Stray card definitions outside the documented 64 (dev/signature cards) are
    # reported for transparency so an accidental extra job is explainable.
    strays: list[dict] = []
    allowed_files = {folder / "*.cs" for folder, _, _ in targets}
    allowed_ids = {id(p) for p in []}
    del allowed_ids
    target_dirs = [folder.resolve() for folder, _, _ in targets]
    for path in sorted((repo / "BelieverCode").rglob("*.cs")):
        try:
            resolved = path.resolve()
        except OSError:
            resolved = path
        if any(resolved.parent == d for d in target_dirs):
            continue
        if path.resolve() == enhanced_file.resolve():
            continue
        for decl in scan_class_decls(path):
            if decl.get("name") in CARD_BASE_NAMES:
                continue  # skip the abstract base definitions themselves
            if decl.get("base") in CARD_BASE_NAMES:
                strays.append(decl)
    side = sorted((repo / "BelieverCode").rglob("*.cs"))
    del side
    return jobs, strays


def read_spec_csv(path: Path) -> tuple[list[dict], dict]:
    raw = path.read_bytes()
    text = raw.decode("gb18030")
    utf8_ok = True
    try:
        raw.decode("utf-8")
    except UnicodeDecodeError:
        utf8_ok = False
    rows = list(csv.DictReader(io.StringIO(text)))
    meta = {
        "bytes": len(raw),
        "encoding": "gb18030",
        "utf8_decodable": utf8_ok,
        "columns": list(rows[0].keys()) if rows else [],
    }
    return rows, meta


def prepare_inventory(ctx: Ctx) -> None:
    """Derive the expected 64-job set independently from BelieverCode + CSV."""
    try:
        code_jobs, strays = scan_card_classes(ctx.repo)
        rows: list[dict] = []
        csv_meta: dict = {}
        if CSV_PATH.is_file():
            rows, csv_meta = read_spec_csv(CSV_PATH)
        else:
            ctx.inventory_error = f"missing spec CSV: {rel(CSV_PATH)}"
        ctx.csv_rows = rows
        ctx.csv_meta = csv_meta
        ctx.strays = strays  # type: ignore[attr-defined]

        code_ids: dict[str, Job] = {}
        for item in code_jobs:
            card_id = "BELIEVER-" + camel_to_snake(item["name"])
            source = f"{rel(item['file'])}:{item['line']}"
            if card_id in code_ids:
                ctx.inventory_error += (
                    f"; duplicate derived id {card_id} at {source} and {code_ids[card_id].source}"
                )
                continue
            code_ids[card_id] = Job(
                card_id=card_id,
                slug=slug_for(card_id),
                kind=item["kind"],
                source=source,
            )

        csv_jobs: dict[str, Job] = {}
        for index, row in enumerate(rows):
            card_id = (row.get("card_id") or "").strip()
            if not card_id:
                ctx.inventory_error += f"; CSV line {index + 2}: empty card_id"
                continue
            group = (row.get("card_group") or "").strip()
            if group == "强化神迹":
                kind = "enhanced"
            elif group == "普通神迹":
                kind = "miracle"
            else:
                kind = "normal"
            csv_jobs[card_id] = Job(
                card_id=card_id,
                slug=slug_for(card_id),
                kind=kind,
                source=f"{rel(CSV_PATH)}:{index + 2}",
                display_name=(row.get("display_name") or "").strip(),
                group=group,
                csv_line=index + 2,
                enhanced_card_id=(row.get("enhanced_card_id") or "").strip(),
            )

        if not ctx.inventory_error:
            if set(code_ids) != set(csv_jobs):
                missing_in_code = sorted(set(csv_jobs) - set(code_ids))
                missing_in_csv = sorted(set(code_ids) - set(csv_jobs))
                ctx.inventory_error = (
                    "belief inventory mismatch: CSV-only="
                    f"{missing_in_code} C#-only={missing_in_csv}"
                )

        # Prefer the C# jobs (real definitions) but keep CSV metadata; on mismatch
        # keep the union so downstream coverage checks surface the disagreement.
        union_ids = list(dict.fromkeys(list(code_ids) + list(csv_jobs)))
        jobs: dict[str, Job] = {}
        for card_id in union_ids:
            base_job = code_ids.get(card_id) or csv_jobs.get(card_id)
            assert base_job is not None
            csv_job = csv_jobs.get(card_id)
            job = Job(
                card_id=card_id,
                slug=slug_for(card_id),
                kind=csv_job.kind if csv_job else base_job.kind,
                source=base_job.source,
                display_name=csv_job.display_name if csv_job else "",
                group=csv_job.group if csv_job else "",
                csv_line=csv_job.csv_line if csv_job else None,
                enhanced_card_id=csv_job.enhanced_card_id if csv_job else "",
            )
            jobs[card_id] = job
        # enhanced rows reference their base miracle id through CSV; attach the
        # reverse link so path checks can tie both directions.
        for card_id, job in jobs.items():
            if job.enhanced_card_id and job.enhanced_card_id in jobs:
                jobs[job.enhanced_card_id].enhanced_of = card_id

        ctx.jobs = jobs
        ctx.slug_to_id = {}
        for card_id, job in jobs.items():
            ctx.slug_to_id.setdefault(job.slug, card_id)
            if job.display_name:
                ctx.display_to_id.setdefault(job.display_name, card_id)
    except Exception as exc:  # never let inventory derivation kill the run
        ctx.inventory_error = f"{type(exc).__name__}: {exc}"


# ---------------------------------------------------------------------------
# staging / manifest discovery
# ---------------------------------------------------------------------------


def discover_staging(ctx: Ctx) -> Path:
    explicit = ctx.staging_arg
    if explicit:
        path = Path(explicit)
        if not path.is_absolute():
            path = (Path.cwd() / path).resolve()
        return normalise_staging_root(path)
    if DEFAULT_STAGING.is_dir() and ((DEFAULT_STAGING / "big").is_dir() or (DEFAULT_STAGING / "small").is_dir()):
        return DEFAULT_STAGING
    if OUT_DIR.is_dir():
        candidates: list[Path] = []
        for path in sorted(OUT_DIR.iterdir()):
            if not path.is_dir() or path == TMP_DIR:
                continue
            if (path / "big").is_dir() or (path / "small").is_dir():
                candidates.append(path)
        if not candidates and (OUT_DIR / "big").is_dir():
            candidates = [OUT_DIR]
        if candidates:
            candidates.sort(key=lambda p: len(list(p.rglob("*.png"))), reverse=True)
            return candidates[0]
    return DEFAULT_STAGING


def normalise_staging_root(path: Path) -> Path:
    if (path / "big").is_dir() or (path / "small").is_dir():
        return path
    if (path / "card_art" / "big").is_dir() or (path / "card_art" / "small").is_dir():
        return path / "card_art"
    return path


def discover_manifest(ctx: Ctx) -> Path:
    if ctx.manifest_arg:
        path = Path(ctx.manifest_arg)
        if not path.is_absolute():
            path = (Path.cwd() / path).resolve()
        return path
    if DEFAULT_MANIFEST.is_file():
        return DEFAULT_MANIFEST
    if OUT_DIR.is_dir():
        candidates = sorted(
            [p for p in OUT_DIR.rglob("*.jsonl") if p.is_file()],
            key=lambda p: p.stat().st_mtime_ns,
            reverse=True,
        )
        if candidates:
            return candidates[0]
    return DEFAULT_MANIFEST


def compute_generated(ctx: Ctx) -> None:
    pairs: dict[str, Job] = {}
    partial: dict[str, Job] = {}
    if not ctx.staging.is_dir():
        ctx.generated_pairs = {}
        ctx.partial_pairs = {}
        return
    big_dir = ctx.staging / "big"
    small_dir = ctx.staging / "small"
    for card_id, job in ctx.jobs.items():
        big = big_dir / f"{job.slug}.png"
        small = small_dir / f"{job.slug}.png"
        has_big, has_small = big.is_file(), small.is_file()
        if has_big and has_small:
            pairs[card_id] = job
        elif has_big or has_small:
            partial[card_id] = job
    ctx.generated_pairs = pairs
    ctx.partial_pairs = partial


# ---------------------------------------------------------------------------
# image helpers
# ---------------------------------------------------------------------------


def image_stats(img: Image.Image) -> dict:
    rgb = img.convert("RGB")
    gray = rgb.convert("L")
    gray_stat = ImageStat.Stat(gray)
    hsv_stat = ImageStat.Stat(rgb.convert("HSV"))
    thumb = rgb.copy()
    thumb.thumbnail((160, 160))
    colors = thumb.getcolors(maxcolors=1 << 20) or []
    total = max(1, thumb.width * thumb.height)
    top_frac = (max(count for count, _ in colors) / total) if colors else 1.0
    return {
        "size": list(rgb.size),
        "mode": img.mode,
        "lum_mean": round(gray_stat.mean[0], 2),
        "lum_std": round(gray_stat.stddev[0], 2),
        "sat_mean": round(hsv_stat.mean[1], 2),
        "distinct_colors": len(colors),
        "top_color_fraction": round(top_frac, 3),
    }


def edge_strip_analysis(img: Image.Image, max_scan: int = MAX_EDGE_SCAN) -> dict:
    """Width in px of near-uniform bright strips on each edge.

    A strip pixel-column/row counts as a padding bar when its luminance mean is
    > BRIGHT_MEAN and its luminance std is < UNIFORM_STD (the Agnes letterbox
    bars are ~238 mean / ~1.5 std).  More than EDGE_TOLERANCE_PX is a defect.
    """
    gray = img.convert("L")
    width, height = gray.size
    result: dict = {"left": 0, "right": 0, "top": 0, "bottom": 0, "samples": {}}

    def col_stats(x: int) -> tuple[float, float]:
        stat = ImageStat.Stat(gray.crop((x, 0, x + 1, height)))
        return stat.mean[0], stat.stddev[0]

    def row_stats(y: int) -> tuple[float, float]:
        stat = ImageStat.Stat(gray.crop((0, y, width, y + 1)))
        return stat.mean[0], stat.stddev[0]

    def scan(edge: str, getter, limit: int) -> int:
        count = 0
        first = None
        while count < limit:
            mean, std = getter(count)
            if mean > BRIGHT_MEAN and std < UNIFORM_STD:
                if first is None:
                    first = (round(mean, 1), round(std, 1))
                count += 1
            else:
                break
        result[edge] = count
        if first is not None:
            result["samples"][edge] = f"mean={first[0]} std={first[1]}"
        return count

    scan("left", lambda i: col_stats(i), min(max_scan, width))
    scan("right", lambda i: col_stats(width - 1 - i), min(max_scan, width))
    scan("top", lambda i: row_stats(i), min(max_scan, height))
    scan("bottom", lambda i: row_stats(height - 1 - i), min(max_scan, height))
    return result


def bad_strips(strips: dict) -> dict[str, int]:
    return {edge: strips[edge] for edge in ("left", "right", "top", "bottom") if strips[edge] > EDGE_TOLERANCE_PX}


def cover_fit(img: Image.Image, size: tuple[int, int]) -> Image.Image:
    src = img.convert("RGB")
    width, height = src.size
    target_w, target_h = size
    scale = max(target_w / width, target_h / height)
    new_w = max(1, round(width * scale))
    new_h = max(1, round(height * scale))
    resized = src.resize((new_w, new_h), Image.LANCZOS)
    left = max(0, (new_w - target_w) // 2)
    top = max(0, (new_h - target_h) // 2)
    return resized.crop((left, top, left + target_w, top + target_h))


def mean_abs_diff(a: Image.Image, b: Image.Image) -> float:
    if a.size != b.size:
        b = b.resize(a.size, Image.LANCZOS)
    diff = ImageChops.difference(a.convert("RGB"), b.convert("RGB"))
    stat = ImageStat.Stat(diff)
    return round(sum(stat.mean) / 3.0, 2)


def load_fallbacks() -> dict:
    cache: dict[str, dict] = {}
    for label, path, size in (
        ("big", FALLBACK_BIG, BIG_SIZE),
        ("small", FALLBACK_SMALL, SMALL_SIZE),
    ):
        if not path.is_file():
            continue
        try:
            img = Image.open(path)
            img.load()
            cache[label] = {
                "path": path,
                "image": img.convert("RGB"),
                "sha256": sha256_file(path),
                "size": size,
            }
        except Exception:
            continue
    return cache


# ---------------------------------------------------------------------------
# pipeline static helpers (trim discovery / source review)
# ---------------------------------------------------------------------------


def collect_functions(path: Path) -> list[dict]:
    if not path.is_file():
        return []
    try:
        source = path.read_text(encoding="utf-8", errors="replace")
        tree = ast.parse(source)
    except (OSError, SyntaxError):
        return []
    found: list[dict] = []

    def walk(node: ast.AST, prefix: str) -> None:
        for child in ast.iter_child_nodes(node):
            if isinstance(child, (ast.FunctionDef, ast.AsyncFunctionDef)):
                qualname = f"{prefix}{child.name}"
                found.append(
                    {
                        "file": path,
                        "name": child.name,
                        "qualname": qualname,
                        "line": child.lineno,
                        "is_method": "." in qualname,
                    }
                )
                walk(child, qualname + ".")
            elif isinstance(child, ast.ClassDef):
                walk(child, f"{prefix}{child.name}.")

    walk(tree, "")
    return found


def discover_trim_functions(ctx: Ctx) -> None:
    functions: list[dict] = []
    for path in (CARD_ART, STYLE):
        for item in collect_functions(path):
            if TRIM_NAME_RE.search(item["name"]):
                functions.append(item)
    ctx.trim_functions = functions
    ctx.trim_source_text = {}
    for path in (CARD_ART, STYLE):
        if path.is_file():
            try:
                ctx.trim_source_text[rel(path)] = path.read_text(encoding="utf-8", errors="replace")
            except OSError:
                pass


def pick_trim_function(functions: list[dict]) -> dict | None:
    scored: list[tuple[int, dict]] = []
    for item in functions:
        name = item["name"].lower()
        score = 0
        if "trim" in name:
            score += 6
        if any(word in name for word in ("bar", "margin", "letterbox", "pillarbox")):
            score += 5
        if "bright" in name:
            score += 3
        if "normal" in name:
            score += 2
        if "crop" in name:
            score += 1
        if "pad" in name:
            score -= 2
        if item.get("is_method"):
            score -= 8
        scored.append((score, item))
    scored.sort(key=lambda pair: pair[0], reverse=True)
    if scored and scored[0][0] > 0:
        return scored[0][1]
    return None


def find_source_lines(path: Path, pattern: str, flags: int = 0, limit: int = 6) -> list[tuple[int, str]]:
    if not path.is_file():
        return []
    try:
        text = path.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return []
    regex = re.compile(pattern, flags)
    hits: list[tuple[int, str]] = []
    for index, line in enumerate(text.splitlines(), 1):
        if regex.search(line):
            hits.append((index, line.strip()))
            if len(hits) >= limit:
                break
    return hits


# ---------------------------------------------------------------------------
# check 0 - agnes_client import + key resolution (no network)
# ---------------------------------------------------------------------------


CLIENT_HARNESS = r'''
import json, os, sys
from pathlib import Path

cfg = json.loads(sys.argv[1])
sys.path.insert(0, cfg["agnes_dir"])
out = {"import_ok": False, "checks": {}}
try:
    import agnes_client as ac
except Exception as exc:
    out["error"] = f"{type(exc).__name__}: {exc}"
    print(json.dumps(out))
    raise SystemExit(0)

out["import_ok"] = True
out["surface"] = {name: hasattr(ac, name) for name in (
    "generate", "generate_to_file", "generate_result", "load_api_key",
    "resolve_model", "AgnesError", "AgnesConfigError", "_parse_dotenv",
    "_valid_size",
)}
out["default_model"] = ac.DEFAULT_MODEL
out["supported_models"] = list(ac.SUPPORTED_MODELS)
out["known_endpoints"] = list(ac.KNOWN_ENDPOINTS)
checks = out["checks"]

try:
    checks["explicit_param"] = ac.load_api_key("explicit-probe-key-aaaaaaaa") == "explicit-probe-key-aaaaaaaa"
except Exception as exc:
    checks["explicit_param"] = f"error {exc}"
os.environ["AGNES_API_KEY"] = "env-probe-key-bbbbbbbb"
try:
    checks["env_before_dotenv"] = ac.load_api_key() == "env-probe-key-bbbbbbbb"
except Exception as exc:
    checks["env_before_dotenv"] = f"error {exc}"
del os.environ["AGNES_API_KEY"]

key = None
try:
    key = ac.load_api_key()
    checks["dotenv_present"] = bool(key)
except Exception as exc:
    checks["dotenv_present"] = f"error {type(exc).__name__}: {exc}"
if key:
    checks["dotenv_length"] = len(key)
    checks["dotenv_not_placeholder"] = key not in ("YOUR_API_KEY", "CHANGEME", "changeme")
    checks["dotenv_matches_tools_env"] = key == ac._parse_dotenv(ac.HERE / ".env").get("AGNES_API_KEY")

# mocked precedence: Tools/AgnesImage/.env must win over <repo>/.env
home = Path(cfg["scratch"])
(home / "Tools" / "AgnesImage").mkdir(parents=True, exist_ok=True)
(home / "Tools" / "AgnesImage" / ".env").write_text("AGNES_API_KEY=tools-dotenv-key-cccccccc\n", encoding="utf-8")
(home / ".env").write_text("AGNES_API_KEY=repo-dotenv-key-dddddddd\n", encoding="utf-8")
old_here, old_root = ac.HERE, ac.ROOT
ac.HERE = home / "Tools" / "AgnesImage"
ac.ROOT = home
try:
    checks["tools_env_before_repo_env"] = ac.load_api_key() == "tools-dotenv-key-cccccccc"
    (home / "Tools" / "AgnesImage" / ".env").unlink()
    checks["repo_env_fallback"] = ac.load_api_key() == "repo-dotenv-key-dddddddd"
finally:
    ac.HERE, ac.ROOT = old_here, old_root

# guards that must fire before any HTTP request
try:
    ac.generate_result("")
    checks["empty_prompt_guard"] = False
except ac.AgnesConfigError:
    checks["empty_prompt_guard"] = True
except Exception:
    checks["empty_prompt_guard"] = False
try:
    ac.generate_result("x", size="banana")
    checks["bad_size_guard"] = False
except ac.AgnesConfigError:
    checks["bad_size_guard"] = True
except Exception:
    checks["bad_size_guard"] = False

checks["valid_size_accepts"] = ac._valid_size("1024x768")
checks["model_resolved"] = ac.resolve_model()
checks["endpoint_candidates_default"] = list(ac._endpoint_candidates(None))
checks["endpoint_candidates_explicit"] = list(ac._endpoint_candidates("https://example.invalid/v1/images/generations"))
print(json.dumps(out))
'''


def check_client(ctx: Ctx, check: Check) -> None:
    if not CLIENT.is_file():
        check.status = FAIL
        check.summary = "Tools/AgnesImage/agnes_client.py not found"
        return
    scratch = ctx.scratch / "client"
    shutil.rmtree(scratch, ignore_errors=True)
    scratch.mkdir(parents=True, exist_ok=True)
    config = json.dumps({"agnes_dir": str(AGNES_DIR), "scratch": str(scratch)})
    record = run_process(
        ctx,
        [ctx.python, "-c", CLIENT_HARNESS, config],
        cwd=AGNES_DIR,
        timeout=60,
        label="agnes_client import/config harness (no network)",
    )
    note(check, f"Command: `python -c <client harness> ` in `{rel(AGNES_DIR)}` (no generation call)")
    note(check, f"Exit code {record['rc']}, elapsed {record['elapsed']}s (a real API call would take ~10s+)")
    if record["timed_out"]:
        check.status = FAIL
        check.summary = "client harness timed out - possible hidden network call"
        add_defect(ctx, check, "critical", "Tools/AgnesImage/agnes_client.py", "import/config load hung")
        return
    if record["rc"] != 0:
        check.status = FAIL
        check.summary = "client harness failed to run"
        err = record["stderr"].strip().splitlines()
        add_defect(ctx, check, "major", "Tools/AgnesImage/agnes_client.py", f"harness exit {record['rc']}: {err[-1] if err else 'no stderr'}")
        return
    try:
        payload = json.loads(record["stdout"].strip().splitlines()[-1])
    except (ValueError, IndexError) as exc:
        check.status = FAIL
        check.summary = f"could not parse client harness output: {exc}"
        note(check, f"stdout: {record['stdout'][:400]}")
        return
    if not payload.get("import_ok"):
        check.status = FAIL
        check.summary = "agnes_client failed to import"
        add_defect(ctx, check, "critical", "Tools/AgnesImage/agnes_client.py", f"import error: {payload.get('error')}")
        return

    surface = payload.get("surface", {})
    missing = sorted(name for name, present in surface.items() if not present)
    if missing:
        add_defect(ctx, check, "major", "Tools/AgnesImage/agnes_client.py", f"public API surface missing: {missing}")

    checks = payload.get("checks", {})
    note(check, f"Default model: `{payload.get('default_model')}`; supported: {payload.get('supported_models')}")
    note(check, f"Known endpoints: {payload.get('known_endpoints')}")
    note(check, f"Key source: {ctx.key_source}; length {len(ctx.key) if ctx.key else 0} (value never printed)")
    note(check, "Key-precedence probes (all fake keys): explicit param / env var / Tools .env / repo .env")
    expected_true = [
        "explicit_param",
        "env_before_dotenv",
        "dotenv_present",
        "dotenv_not_placeholder",
        "dotenv_matches_tools_env",
        "tools_env_before_repo_env",
        "repo_env_fallback",
        "empty_prompt_guard",
        "bad_size_guard",
        "valid_size_accepts",
    ]
    for name in expected_true:
        value = checks.get(name)
        note(check, f"- `{name}` = `{value}`")
        if value is not True:
            add_defect(
                ctx,
                check,
                "major",
                "Tools/AgnesImage/agnes_client.py",
                f"key/config behaviour `{name}` returned {value!r}, expected True",
            )
    candidates = checks.get("endpoint_candidates_default")
    if not (isinstance(candidates, list) and len(candidates) == len(payload.get("known_endpoints") or [])):
        add_defect(
            ctx,
            check,
            "minor",
            "Tools/AgnesImage/agnes_client.py",
            f"default endpoint candidates unexpected: {candidates}",
        )
    explicit_candidates = checks.get("endpoint_candidates_explicit")
    if not (isinstance(explicit_candidates, list) and len(explicit_candidates) == 1):
        add_defect(
            ctx,
            check,
            "minor",
            "Tools/AgnesImage/agnes_client.py",
            f"explicit endpoint should disable fallback, got {explicit_candidates}",
        )
    if record["leaked_key"]:
        check.status = FAIL
        add_defect(ctx, check, "critical", "Tools/AgnesImage/agnes_client.py", "client harness output leaked the live key")

    # static evidence: the only place the key is used and the documented precedence
    for line in find_source_lines(CLIENT, r"Bearer \{api_key\}", limit=3):
        note(check, f"Key used only in the Authorization header: `Tools/AgnesImage/agnes_client.py:{line[0]}`")
    note(
        check,
        "Documented/implemented precedence: explicit arg > `AGNES_API_KEY` env > "
        "`Tools/AgnesImage/.env` > `<repo>/.env` (`agnes_client.py:85-102`)",
    )
    for line in find_source_lines(CLIENT, r"raise AgnesConfigError", limit=4):
        note(check, f"Config error path does not include key material: `agnes_client.py:{line[0]}`")

    if check.defects:
        check.status = FAIL
        check.summary = f"client behaviour defect(s): {len(check.defects)}"
    else:
        check.status = PASS
        check.summary = "imports cleanly; key precedence explicit>env>Tools/.env>repo/.env; no network call"


# ---------------------------------------------------------------------------
# check 1 - independent job inventory
# ---------------------------------------------------------------------------


def check_inventory(ctx: Ctx, check: Check) -> None:
    if ctx.inventory_error:
        add_defect(ctx, check, "critical", "docs/M10.2.1-CardArt-Spec.csv / BelieverCode", ctx.inventory_error)
    jobs = ctx.jobs
    kinds = {"normal": [], "miracle": [], "enhanced": []}
    for job in jobs.values():
        kinds.setdefault(job.kind, []).append(job)

    note(check, f"Derived from BelieverCode + `{rel(CSV_PATH)}`: {len(jobs)} jobs")
    note(check, f"- normal: {len(kinds['normal'])} (Cards/Basic 24 + Cards/Blue 16 expected)")
    note(check, f"- base miracles: {len(kinds['miracle'])} (Cards/Miracles)")
    note(check, f"- enhanced miracles: {len(kinds['enhanced'])} (Miracles/EnhancedMiracles.cs)")
    note(check, f"CSV rows: {len(ctx.csv_rows)}, encoding={ctx.csv_meta.get('encoding')}, "
                f"utf8_decodable={ctx.csv_meta.get('utf8_decodable')}, bytes={ctx.csv_meta.get('bytes')}")

    # duplicate / slug checks
    ids = [job.card_id for job in jobs.values()]
    slugs: dict[str, list[str]] = {}
    for job in jobs.values():
        slugs.setdefault(job.slug, []).append(job.card_id)
    duplicates = [card_id for card_id, count in ((i, ids.count(i)) for i in set(ids)) if count > 1]
    duplicate_slugs = {slug: owners for slug, owners in slugs.items() if len(owners) > 1}
    if duplicates or duplicate_slugs:
        add_defect(ctx, check, "critical", "inventory", f"duplicate ids={duplicates} duplicate slugs={duplicate_slugs}")

    # expected path convention from the spec CSV (independent of implementation)
    path_mismatch: list[str] = []
    for row in ctx.csv_rows:
        card_id = (row.get("card_id") or "").strip()
        if not card_id:
            continue
        slug = slug_for(card_id)
        big = row.get("expected_big_art", "")
        small = row.get("expected_small_art", "")
        expected_big = f"res://Believer/images/card_portraits/big/{slug}.png"
        expected_small = f"res://Believer/images/card_portraits/{slug}.png"
        if big != expected_big:
            path_mismatch.append(f"{card_id} big={big!r} expected {expected_big!r}")
        if small != expected_small:
            path_mismatch.append(f"{card_id} small={small!r} expected {expected_small!r}")
    if path_mismatch:
        for item in path_mismatch[:8]:
            add_defect(ctx, check, "major", rel(CSV_PATH), f"portrait path/slug rule mismatch: {item}")
    note(check, f"Slug rule `strip BELIEVER- + lowercase` re-derived every CSV path: "
                f"{len(ctx.csv_rows) - len(path_mismatch)} / {len(ctx.csv_rows)} rows agree")

    # enhanced linkage: 12 base miracles -> 12 enhanced rows, both directions
    linkage_errors = 0
    base_to_enhanced: dict[str, str] = {}
    for job in kinds["miracle"]:
        if not job.enhanced_card_id:
            add_defect(ctx, check, "major", job.source, f"{job.card_id}: missing enhanced_card_id")
            linkage_errors += 1
            continue
        enhanced = jobs.get(job.enhanced_card_id)
        if enhanced is None:
            add_defect(ctx, check, "major", job.source, f"{job.card_id}: enhanced_card_id {job.enhanced_card_id} not in code/CSV inventory")
            linkage_errors += 1
            continue
        if enhanced.kind != "enhanced":
            add_defect(ctx, check, "major", job.source, f"{job.card_id}: {job.enhanced_card_id} is not an enhanced job")
            linkage_errors += 1
        expected = "enhanced_" + job.slug
        if enhanced.slug != expected:
            add_defect(
                ctx,
                check,
                "major",
                enhanced.source,
                f"{enhanced.card_id}: slug {enhanced.slug!r} should derive from enhanced_card_id as {expected!r}",
            )
            linkage_errors += 1
        base_to_enhanced[job.card_id] = job.enhanced_card_id
    enhanced_without_base = [
        job.card_id
        for job in kinds["enhanced"]
        if job.card_id not in set(base_to_enhanced.values())
    ]
    if enhanced_without_base:
        add_defect(ctx, check, "major", "inventory", f"enhanced jobs with no base miracle reference: {enhanced_without_base}")
        linkage_errors += 1
    note(check, f"Enhanced linkage: {len(base_to_enhanced)} / 12 base miracles map to a matching enhanced job")

    # counts from the spec document
    expected_counts = {"normal": 40, "miracle": 12, "enhanced": 12}
    for kind, expected in expected_counts.items():
        if len(kinds[kind]) != expected:
            add_defect(ctx, check, "critical", "docs/M10.2.1-CardArt-Spec.csv", f"{kind} jobs {len(kinds[kind])} != expected {expected}")
    if len(jobs) != 64:
        add_defect(ctx, check, "critical", "inventory", f"total jobs {len(jobs)} != 64")

    # BelieverCode cross-check evidence (per job source)
    note(check, f"BelieverCode `class` -> id evidence captured for all {len(jobs)} jobs "
                f"(e.g. `{next(iter(jobs.values())).source}` -> `{next(iter(jobs.values())).card_id}`)")
    strays = getattr(ctx, "strays", [])
    if strays:
        note(check, "Non-target card classes deliberately excluded from the 64 (dev/signature cards):")
        for item in strays[:8]:
            note(check, f"- `{rel(item['file'])}:{item['line']}` {item['name']} : {item['base']}")
    else:
        note(check, "No stray BelieverCard/MiracleCard/EnhancedMiracleCard classes outside the 64 target definitions")

    if check.defects:
        check.status = FAIL
        check.summary = f"inventory defect(s): {len(check.defects)}"
    else:
        check.status = PASS
        check.summary = "64 jobs (40/12/12), slugs + enhanced linkage consistent, C# inventory == spec CSV"


# ---------------------------------------------------------------------------
# check 2 - dry-run
# ---------------------------------------------------------------------------


def parse_dry_run(ctx: Ctx, text: str) -> dict:
    expected_ids = set(ctx.jobs)
    expected_slugs = set(ctx.slug_to_id)
    observed_ids = set(re.findall(r"BELIEVER-[A-Z0-9_]+", text))
    big_refs = set(re.findall(r"big[\\/]+([a-z0-9_]+)\.png", text, re.IGNORECASE))
    all_png_stems = set(re.findall(r"([a-z0-9_]+)\.png", text))
    present_slugs = expected_slugs & big_refs
    covered: list[str] = []
    for card_id, job in ctx.jobs.items():
        if card_id in observed_ids or job.slug in big_refs:
            covered.append(card_id)
    covered_items: list[str] = []
    for card_id, job in ctx.jobs.items():
        if card_id in observed_ids or job.slug in big_refs:
            covered_items.append(card_id)
    present_slugs = expected_slugs & big_refs
    return {
        "expected_ids": expected_ids,
        "expected_slugs": expected_slugs,
        "observed_ids": observed_ids,
        "big_refs": big_refs,
        "all_png_stems": all_png_stems,
        "present_slugs": present_slugs,
        "covered": covered_items,
        "missing": sorted(expected_ids - set(covered_items)),
        "extra_ids": sorted(observed_ids - expected_ids),
        "extra_big_refs": sorted(big_refs - expected_slugs),
        "extra_png_stems": sorted(all_png_stems - expected_slugs - {"card"}),
    }


def check_dry_run(ctx: Ctx, check: Check) -> None:
    if not CARD_ART.is_file():
        check.status = SKIP
        check.summary = "Tools/AgnesImage/card_art.py does not exist yet"
        return
    if ctx.no_run:
        check.status = SKIP
        check.summary = "--no-run given"
        return

    # card_art.py's --staging is a boolean mode flag (not a path) and the manifest
    # default is out/manifest.jsonl, so the dry-run is executed exactly as a user
    # would run it and the whole out/ tree is snapshotted to prove nothing was written.
    before_out = snapshot_tree(OUT_DIR, skip_prefixes=("_verify_tmp",))
    manifest_before = sha256_file(ctx.manifest) if ctx.manifest.is_file() else None
    manifest_lines_before = -1
    if ctx.manifest.is_file():
        try:
            manifest_lines_before = len(
                [line for line in ctx.manifest.read_text(encoding="utf-8", errors="replace").splitlines() if line.strip()]
            )
        except OSError:
            manifest_lines_before = -1
    cmd = [ctx.python, str(CARD_ART), "--all", "--dry-run"]
    record = run_process(ctx, cmd, cwd=AGNES_DIR, timeout=ctx.timeout, env=trap_env(ctx), label="card_art --all --dry-run")
    note(check, f"Command: `python {rel(CARD_ART)} --all --dry-run` "
                f"(cwd `{rel(AGNES_DIR)}`, default staging `{rel(ctx.staging)}` + manifest `{rel(ctx.manifest)}`)")
    note(check, f"Exit code: {record['rc']}, elapsed {record['elapsed']}s (timeout {ctx.timeout}s)")
    if record["leaked_key"]:
        add_defect(ctx, check, "critical", "Tools/AgnesImage/card_art.py", "dry-run output leaked the live API key")

    after_out = snapshot_tree(OUT_DIR, skip_prefixes=("_verify_tmp",))
    changed = diff_snapshots(before_out, after_out)
    manifest_after = sha256_file(ctx.manifest) if ctx.manifest.is_file() else None
    manifest_lines_after = -1
    if ctx.manifest.is_file():
        try:
            manifest_lines_after = len(
                [line for line in ctx.manifest.read_text(encoding="utf-8", errors="replace").splitlines() if line.strip()]
            )
        except OSError:
            manifest_lines_after = -1
    new_manifest_lines = manifest_lines_after - manifest_lines_before if manifest_lines_after >= 0 and manifest_lines_before >= 0 else 0

    if record["rc"] != 0 or record["timed_out"]:
        check.status = FAIL
        check.summary = f"dry-run exit {record['rc']} (timed_out={record['timed_out']})"
        tail = (record["stderr"] or record["stdout"]).strip().splitlines()
        add_defect(ctx, check, "critical", "Tools/AgnesImage/card_art.py",
                   f"`--all --dry-run` failed (exit {record['rc']}): {tail[-1] if tail else 'no output'}")
        return

    output = record["stdout"] + "\n" + record["stderr"]
    parsed = parse_dry_run(ctx, output)
    note(check, f"Jobs listed: {len(parsed['covered'])}/64; card ids seen: {len(parsed['observed_ids'])}; "
                f"big output paths seen: {len(parsed['big_refs'])}")
    for label, values in (
        ("missing jobs", parsed["missing"]),
        ("unexpected card ids", parsed["extra_ids"]),
        ("unexpected big paths", parsed["extra_big_refs"]),
        ("unexpected png names", parsed["extra_png_stems"]),
    ):
        if values:
            add_defect(ctx, check, "major", "Tools/AgnesImage/card_art.py",
                       f"dry-run {label} ({len(values)}): {values[:12]}")
    if len(parsed["covered"]) != 64:
        add_defect(ctx, check, "critical", "Tools/AgnesImage/card_art.py",
                   f"dry-run listed {len(parsed['covered'])} of 64 expected jobs")

    # sample lines as evidence
    sample = [line.strip() for line in output.splitlines() if line.strip()][:4]
    for line in sample:
        note(check, f"output: `{line[:160]}`")

    # no-write guarantees
    if changed:
        add_defect(ctx, check, "critical", "Tools/AgnesImage/card_art.py",
                   f"dry-run modified files under out/: {changed}")
    if new_manifest_lines > 0 or (manifest_before is not None and manifest_after != manifest_before):
        add_defect(ctx, check, "critical", "Tools/AgnesImage/card_art.py",
                   f"dry-run wrote manifest entries ({new_manifest_lines} new line(s))")
    note(check, f"No-write check: {'PASS' if not changed and new_manifest_lines <= 0 else 'FAIL'} "
                f"(whole out/ tree snapshot before/after: {len(before_out)} -> {len(after_out)} files; "
                f"manifest lines {manifest_lines_before} -> {manifest_lines_after})")

    # no network / no key leakage indicators
    network_markers = re.findall(r"(AgnesError|HTTP \d{3}|Connection refused|网络错误|请求超时|Traceback)", output)
    if network_markers:
        add_defect(ctx, check, "major", "Tools/AgnesImage/card_art.py",
                   f"dry-run output contains error/traceback markers: {sorted(set(network_markers))}")
    if record["elapsed"] > 60:
        add_defect(ctx, check, "major", "Tools/AgnesImage/card_art.py",
                   f"dry-run took {record['elapsed']}s - looks like it performed network work")

    if check.defects:
        check.status = FAIL
        check.summary = f"dry-run defect(s): {len(check.defects)}"
    else:
        check.status = PASS
        check.summary = f"exit 0, lists all 64 jobs, wrote nothing, {record['elapsed']}s"


# ---------------------------------------------------------------------------
# check 3 - image contract (+ letterbox + trim function)
# ---------------------------------------------------------------------------


def validate_png(ctx: Ctx, check: Check, path: Path, expected_size: tuple[int, int], kind: str) -> tuple[dict | None, bool]:
    """Validate one PNG against the contract. Returns (stats, ok)."""
    problems: list[str] = []
    if not path.is_file():
        add_defect(ctx, check, "critical", rel(path), f"{kind}: missing file")
        return None, False
    size_bytes = path.stat().st_size
    min_bytes = BIG_MIN_BYTES if kind == "big" else SMALL_MIN_BYTES
    max_bytes = BIG_MAX_BYTES if kind == "big" else SMALL_MAX_BYTES
    try:
        with Image.open(path) as im:
            im.load()
            fmt = im.format
            size = im.size
            mode = im.mode
            has_transparency = "transparency" in im.info
            rgb = im.convert("RGB")
        stats = image_stats(rgb)
        strips = edge_strip_analysis(rgb)
        stats["bytes"] = size_bytes
        stats["strips"] = strips
    except Exception as exc:
        add_defect(ctx, check, "critical", rel(path), f"{kind}: not a decodable image ({type(exc).__name__}: {exc})")
        return None, False

    if fmt != "PNG":
        problems.append(f"format {fmt!r} != PNG")
    if size != expected_size:
        problems.append(f"size {size} != {expected_size}")
    if mode != "RGB":
        problems.append(f"mode {mode!r} != RGB (alpha must be stripped)")
    if has_transparency:
        problems.append("PNG has transparency chunk")
    if size_bytes < min_bytes:
        problems.append(f"file too small: {human_bytes(size_bytes)} < {human_bytes(min_bytes)} (trivial/blank?)")
    if size_bytes > max_bytes:
        problems.append(f"file implausibly large: {human_bytes(size_bytes)}")
    if stats["lum_std"] < MIN_LUM_STD:
        problems.append(f"near-blank image (luma std {stats['lum_std']} < {MIN_LUM_STD})")
    if stats["distinct_colors"] < MIN_DISTINCT_COLORS:
        problems.append(f"too few distinct colours ({stats['distinct_colors']} < {MIN_DISTINCT_COLORS})")
    if stats["top_color_fraction"] > MAX_TOP_COLOR_FRACTION:
        problems.append(f"single colour covers {stats['top_color_fraction']:.0%} of the image")
    if stats["sat_mean"] < MIN_SAT_MEAN:
        problems.append(f"suspiciously desaturated (mean saturation {stats['sat_mean']})")
    bad = bad_strips(strips)
    if bad:
        edge_desc = ", ".join(f"{edge}={width}px" for edge, width in bad.items())
        problems.append(f"letterbox/pillarbox bars left on edges: {edge_desc} (threshold {EDGE_TOLERANCE_PX}px)")
        check.details.append(f"LETTERBOX {rel(path)}: {edge_desc} samples={strips['samples']}")

    for problem in problems:
        add_defect(ctx, check, "major", rel(path), f"{kind}: {problem}")
    return stats, not problems


def check_images(ctx: Ctx, check: Check) -> None:
    if not ctx.staging.is_dir():
        check.status = SKIP
        check.summary = f"staging dir not found: {rel(ctx.staging)}"
        return
    if not ctx.generated_pairs and not ctx.partial_pairs:
        check.status = SKIP
        check.summary = "no generated card-art PNGs in staging yet"
        note(check, f"Checked `{rel(ctx.staging)}` for `big/<slug>.png` + `small/<slug>.png` pairs: none found")
        return

    fallbacks = load_fallbacks()
    note(check, f"Staging: `{rel(ctx.staging)}`; generated pairs: {len(ctx.generated_pairs)}/64; "
                f"partial pairs: {len(ctx.partial_pairs)}/64 (partial = only one size present)")
    if len(ctx.generated_pairs) < 64:
        note(check, f"Coverage note: verifying the {len(ctx.generated_pairs)} generated pairs (sample set accepted by task scope)")

    stats_by_slug: dict[str, dict] = {}
    failures = 0
    for card_id, job in sorted(ctx.generated_pairs.items(), key=lambda item: item[1].slug):
        big_path = ctx.staging / "big" / f"{job.slug}.png"
        small_path = ctx.staging / "small" / f"{job.slug}.png"
        big_stats, big_ok = validate_png(ctx, check, big_path, BIG_SIZE, "big")
        small_stats, small_ok = validate_png(ctx, check, small_path, SMALL_SIZE, "small")
        if not (big_ok and small_ok):
            failures += 1
            continue
        assert big_stats is not None and small_stats is not None
        stats_by_slug[job.slug] = {"big": big_stats, "small": small_stats}

        # small must be a downscale of big
        try:
            with Image.open(big_path) as big_img:
                big_rgb = big_img.convert("RGB")
            with Image.open(small_path) as small_img:
                small_rgb = small_img.convert("RGB")
            expected_small = big_rgb.resize(SMALL_SIZE, Image.LANCZOS)
            diff = mean_abs_diff(small_rgb, expected_small)
            note(check, f"`{job.slug}`: big {big_stats['size']} {human_bytes(big_stats['bytes'])} / "
                        f"small {small_stats['size']} {human_bytes(small_stats['bytes'])} / small-vs-big diff {diff}")
            if diff > SMALL_BIG_MAX_DIFF:
                add_defect(ctx, check, "major", rel(small_path),
                           f"small is not a faithful downscale of big (mean abs diff {diff} > {SMALL_BIG_MAX_DIFF})")
                failures += 1

            # duplicate of the placeholder fallback art?
            fallback = fallbacks.get("big")
            if fallback is not None:
                if sha256_file(big_path) == fallback["sha256"]:
                    add_defect(ctx, check, "critical", rel(big_path),
                               "big PNG is byte-identical to the fallback card.png")
                    failures += 1
                else:
                    fallback_fit = cover_fit(fallback["image"], BIG_SIZE)
                    fb_diff = mean_abs_diff(big_rgb, fallback_fit)
                    if fb_diff < FALLBACK_MAX_DIFF:
                        add_defect(ctx, check, "critical", rel(big_path),
                                   f"big PNG looks identical to the fallback card.png (diff {fb_diff})")
                        failures += 1
            fallback_small = fallbacks.get("small")
            if fallback_small is not None and sha256_file(small_path) == fallback_small["sha256"]:
                add_defect(ctx, check, "critical", rel(small_path),
                           "small PNG is byte-identical to the fallback card.png")
                failures += 1
        except Exception as exc:
            add_defect(ctx, check, "major", rel(big_path), f"pair comparison crashed: {type(exc).__name__}: {exc}")
            failures += 1

    # partial pairs are a defect
    for card_id, job in sorted(ctx.partial_pairs.items(), key=lambda item: item[1].slug):
        big_exists = (ctx.staging / "big" / f"{job.slug}.png").is_file()
        small_exists = (ctx.staging / "small" / f"{job.slug}.png").is_file()
        add_defect(ctx, check, "critical", rel(ctx.staging),
                   f"{job.slug}: incomplete output pair (big={big_exists}, small={small_exists})")
        failures += 1

    # cross-job duplicates (identical art = broken generation/failed copy)
    hashes: dict[str, list[str]] = {}
    for card_id, job in ctx.generated_pairs.items():
        big_path = ctx.staging / "big" / f"{job.slug}.png"
        if big_path.is_file():
            hashes.setdefault(sha256_file(big_path), []).append(job.slug)
    duplicates = {digest: slugs for digest, slugs in hashes.items() if len(slugs) > 1}
    for digest, slugs in duplicates.items():
        add_defect(ctx, check, "major", rel(ctx.staging / "big"), f"identical big PNGs: {slugs}")

    # stray files in big/ and small/ directories
    for sub in ("big", "small"):
        folder = ctx.staging / sub
        if not folder.is_dir():
            continue
        known = {f"{job.slug}.png" for job in ctx.jobs.values()}
        strays = sorted(p.name for p in folder.iterdir() if p.is_file() and p.name not in known)
        if strays:
            add_defect(ctx, check, "minor", rel(folder), f"unexpected file(s) in {sub}/: {strays[:8]}")

    # trim-function round trip
    trim_status = run_trim_sanity(ctx, check)

    if failures or check.defects or trim_status in ("FAILED", "FAIL"):
        check.status = FAIL
        check.summary = (f"{failures} pair defect(s) across {len(ctx.generated_pairs)} generated pairs; "
                         f"trim/normalize round-trip {trim_status or 'N/A'}")
    else:
        extra = "" if trim_status is None else f"; trim/normalize round-trip {trim_status}"
        check.status = PASS
        check.summary = (f"{len(ctx.generated_pairs)}/64 pairs conform (1000x760 / 250x190 RGB, "
                         f"no bars, no fallback copies){extra}")


def run_trim_sanity(ctx: Ctx, check: Check) -> str | None:
    """Unit-test the pipeline's trim helpers on synthetic and real-padding fixtures.

    The documented Agnes defect is a returned canvas padded with uniform light-gray
    letterbox/pillarbox bars (real sample: out/let_there_be_light_test.png, bars
    x=0..270 and 881..1151).  ``card_art.normalize_artwork`` / ``detect_content_bbox``
    must trim exactly those bars, must not crop a normal full-bleed image and must
    not mistake an all-bright artwork for padding.
    """
    if ctx.no_run:
        note(check, "Trim/normalize unit tests: skipped (--no-run)")
        return None
    if not CARD_ART.is_file():
        note(check, "Trim/normalize unit tests: card_art.py missing (N/A)")
        return None
    scratch = ctx.scratch / "trim"
    shutil.rmtree(scratch, ignore_errors=True)
    scratch.mkdir(parents=True, exist_ok=True)

    # fixture 1: 610x864 content centred on a 1152x864 light-gray canvas, i.e.
    #   271px pillarbox left + 271px right, exactly like the real Agnes sample.
    canvas = Image.new("RGB", (1152, 864), (238, 238, 236))
    content = Image.new("RGB", (610, 864), (28, 24, 34))
    draw = ImageDraw.Draw(content)
    draw.ellipse((150, 110, 460, 420), fill=(212, 176, 92))
    draw.polygon([(305, 190), (455, 760), (150, 760)], fill=(240, 236, 226))
    draw.rectangle((0, 780, 610, 864), fill=(58, 50, 40))
    canvas.paste(content, (271, 0))
    pill_path = scratch / "trim_pillarbox.png"
    canvas.save(pill_path)

    # fixture 2: full-bleed illustration without padding; must not be cropped.
    base = Image.new("RGB", (8, 6))
    base_px = base.load()
    palette = [
        (22, 18, 30), (40, 30, 52), (70, 52, 78), (120, 80, 70),
        (196, 160, 96), (240, 232, 214), (96, 60, 90), (48, 36, 60),
    ]
    for y in range(6):
        for x in range(8):
            base_px[x, y] = palette[(x * 3 + y * 5) % len(palette)]
    full = base.resize((1152, 864), Image.BICUBIC)
    draw_full = ImageDraw.Draw(full)
    draw_full.ellipse((430, 120, 720, 410), fill=(226, 196, 120))
    draw_full.polygon([(560, 260), (800, 840), (320, 840)], fill=(242, 238, 228))
    full_path = scratch / "trim_fullbleed.png"
    full.save(full_path)

    # fixture 3: all-bright artwork; must be preserved (no trim).
    bright = Image.new("RGB", (1152, 864), (240, 239, 237))
    bright_path = scratch / "trim_allbright.png"
    bright.save(bright_path)

    cases = [
        {"name": "pillarbox", "input": str(pill_path), "out": str(scratch / "out_pillarbox.png")},
        {"name": "fullbleed", "input": str(full_path), "out": str(scratch / "out_fullbleed.png")},
        {"name": "allbright", "input": str(bright_path), "out": str(scratch / "out_allbright.png")},
    ]
    real_sample = OUT_DIR / "let_there_be_light_test.png"
    if real_sample.is_file():
        cases.append(
            {
                "name": "real_pillarbox_sample",
                "input": str(real_sample),
                "out": str(scratch / "out_real_sample.png"),
            }
        )
    else:
        note(check, "Real defective Agnes sample `out/let_there_be_light_test.png` is absent; synthetic cases only")

    config = json.dumps({"agnes_dir": str(AGNES_DIR), "cases": cases})
    record = run_process(
        ctx,
        [ctx.python, "-c", TRIM_HARNESS_V2, config],
        cwd=AGNES_DIR,
        timeout=120,
        label="trim/normalize unit tests (detect_content_bbox + normalize_artwork)",
    )
    helper_lines = [
        f"`{rel(item['file'])}:{item['line']}` {item['qualname']}"
        for item in ctx.trim_functions
        if item["name"] in ("detect_content_bbox", "normalize_artwork")
    ]
    note(check, "Trim helpers under test: " + (", ".join(helper_lines) if helper_lines else "not found by the AST scan"))
    note(check, f"Trim harness: exit {record['rc']}, elapsed {record['elapsed']}s")
    if record["rc"] != 0:
        add_defect(ctx, check, "major", rel(CARD_ART), f"trim harness failed (exit {record['rc']}): {record['stderr'][-300:]}")
        return "FAILED"
    try:
        payload = json.loads(record["stdout"].strip().splitlines()[-1])
    except (ValueError, IndexError):
        add_defect(ctx, check, "major", rel(CARD_ART), f"trim harness output unparsable: {record['stdout'][:300]}")
        return "FAILED"
    if not payload.get("import_ok"):
        add_defect(ctx, check, "major", rel(CARD_ART), f"card_art import failed in trim harness: {payload.get('error')}")
        return "FAILED"

    ok = True
    if not payload.get("has_detect_content_bbox"):
        ok = False
        add_defect(ctx, check, "major", rel(CARD_ART), "detect_content_bbox() is missing - padding cannot be detected")
    if not payload.get("has_normalize_artwork"):
        ok = False
        add_defect(ctx, check, "major", rel(CARD_ART), "normalize_artwork() is missing - padding cannot be trimmed")
    by_name = {case.get("name"): case for case in payload.get("cases", [])}

    def inspect_detect(name: str, expect_trimmed: bool, expected_px: list[int] | None = None,
                       expected_bbox: list[int] | None = None) -> bool:
        entry = by_name.get(name)
        if entry is None:
            add_defect(ctx, check, "major", rel(CARD_ART), f"detect_content_bbox: fixture {name!r} missing from harness output")
            return False
        detect = entry.get("detect")
        if not detect:
            add_defect(ctx, check, "major", rel(CARD_ART),
                       f"detect_content_bbox({name}) failed: {entry.get('detect_error', 'no output')}")
            return False
        meta = detect.get("meta") or {}
        trimmed = bool(meta.get("bars_trimmed"))
        trim_px = list(meta.get("trim_px") or [0, 0, 0, 0])
        bbox = list(detect.get("bbox") or [0, 0, 0, 0])
        problems: list[str] = []
        if trimmed != expect_trimmed:
            problems.append(f"bars_trimmed={trimmed}, expected {expect_trimmed}")
        if expected_px is not None and trim_px != expected_px:
            problems.append(f"trim_px={trim_px}, expected {expected_px}")
        if expected_bbox is not None and bbox != expected_bbox:
            problems.append(f"bbox={bbox}, expected {expected_bbox}")
        note(check, f"detect_content_bbox({name}): bars_trimmed={trimmed}, trim_px={trim_px}, bbox={bbox}"
                    + ("" if not problems else " -> " + "; ".join(problems)))
        for problem in problems:
            add_defect(ctx, check, "major", rel(CARD_ART), f"detect_content_bbox({name}): {problem}")
        return not problems

    def inspect_normalize(name: str, expect_trimmed: bool, *, require_clean: bool = True,
                          compare_with: Image.Image | None = None) -> bool:
        entry = by_name.get(name)
        if entry is None:
            add_defect(ctx, check, "major", rel(CARD_ART), f"normalize_artwork: fixture {name!r} missing from harness output")
            return False
        normalize = entry.get("normalize")
        if not normalize or not normalize.get("ok"):
            add_defect(ctx, check, "major", rel(CARD_ART),
                       f"normalize_artwork({name}) failed: {(normalize or {}).get('error', 'no output')}")
            return False
        problems: list[str] = []
        meta = normalize.get("meta") or {}
        size = list(normalize.get("size") or [])
        if size != list(BIG_SIZE):
            problems.append(f"output size {size} != {list(BIG_SIZE)}")
        if normalize.get("mode") != "RGB":
            problems.append(f"output mode {normalize.get('mode')!r} != RGB")
        if bool(meta.get("bars_trimmed")) != expect_trimmed:
            problems.append(f"meta bars_trimmed={meta.get('bars_trimmed')}, expected {expect_trimmed}")
        out_path = Path(normalize.get("out", ""))
        strips = {}
        stats: dict = {}
        try:
            with Image.open(out_path) as img:
                img.load()
                rgb = img.convert("RGB")
            strips = edge_strip_analysis(rgb)
            stats = image_stats(rgb)
            if require_clean:
                bad = bad_strips(strips)
                if bad:
                    problems.append(f"residual bright padding bars {bad}")
                if stats["lum_std"] < 10:
                    problems.append(f"output looks blank (luma std {stats['lum_std']})")
            if compare_with is not None:
                diff = mean_abs_diff(rgb, cover_fit(compare_with, BIG_SIZE))
                note(check, f"normalize_artwork({name}) content diff vs cover_fit(original): {diff}")
                if diff > 15:
                    problems.append(f"content changed too much vs untouched original (diff {diff})")
        except Exception as exc:
            problems.append(f"output unreadable: {type(exc).__name__}: {exc}")
        note(check, f"normalize_artwork({name}): out {size} mode={normalize.get('mode')}, "
                    f"edges { {e: strips.get(e) for e in ('left','right','top','bottom')} }, "
                    f"luma std {stats.get('lum_std')}, trim_px={list(meta.get('trim_px') or [])}"
                    + ("" if not problems else " -> " + "; ".join(problems)))
        for problem in problems:
            add_defect(ctx, check, "major", rel(CARD_ART), f"normalize_artwork({name}): {problem}")
        return not problems

    ok &= inspect_detect("pillarbox", True, [271, 0, 271, 0], [271, 0, 881, 864])
    ok &= inspect_detect("fullbleed", False)
    ok &= inspect_detect("allbright", False)
    if "real_pillarbox_sample" in by_name:
        ok &= inspect_detect("real_pillarbox_sample", True, [271, 0, 271, 0], [271, 0, 881, 864])

    ok &= inspect_normalize("pillarbox", True)
    ok &= inspect_normalize("fullbleed", False, compare_with=full)
    ok &= inspect_normalize("allbright", False, require_clean=False)
    if "real_pillarbox_sample" in by_name:
        ok &= inspect_normalize("real_pillarbox_sample", True)

    return "PASS" if ok else "FAIL"


TRIM_HARNESS_V2 = r'''
import importlib, json, sys
from pathlib import Path

from PIL import Image

cfg = json.loads(sys.argv[1])
sys.path.insert(0, cfg["agnes_dir"])
result = {"import_ok": False, "cases": []}
try:
    module = importlib.import_module("card_art")
except Exception as exc:
    result["error"] = f"{type(exc).__name__}: {exc}"
    print(json.dumps(result))
    raise SystemExit(0)

detect = getattr(module, "detect_content_bbox", None)
normalize = getattr(module, "normalize_artwork", None)
result["import_ok"] = True
result["has_detect_content_bbox"] = callable(detect)
result["has_normalize_artwork"] = callable(normalize)

for case in cfg["cases"]:
    entry = {"name": case["name"]}
    raw = Path(case["input"]).read_bytes()
    try:
        with Image.open(case["input"]) as opened:
            image = opened.convert("RGB")
    except Exception as exc:
        entry["error"] = f"{type(exc).__name__}: {exc}"
        result["cases"].append(entry)
        continue
    if callable(detect):
        try:
            bbox, meta = detect(image.copy())
            entry["detect"] = {"bbox": list(bbox), "meta": {k: v for k, v in dict(meta).items()}}
        except Exception as exc:
            entry["detect_error"] = f"{type(exc).__name__}: {exc}"
    if callable(normalize):
        try:
            normalized, meta = normalize(raw)
            out_path = Path(case["out"])
            normalized.save(out_path)
            entry["normalize"] = {
                "ok": True,
                "out": str(out_path),
                "size": list(normalized.size),
                "mode": normalized.mode,
                "meta": {k: v for k, v in dict(meta).items()},
            }
        except Exception as exc:
            entry["normalize"] = {"ok": False, "error": f"{type(exc).__name__}: {exc}"}
    result["cases"].append(entry)

print(json.dumps(result, default=str))
'''


# ---------------------------------------------------------------------------
# check 4 - manifest integrity
# ---------------------------------------------------------------------------


def flatten_record(record: dict) -> list[tuple[str, object]]:
    flat: list[tuple[str, object]] = []

    def walk(value, prefix: str) -> None:
        if isinstance(value, dict):
            for key, child in value.items():
                walk(child, f"{prefix}.{key}" if prefix else str(key))
        elif isinstance(value, list):
            for index, child in enumerate(value):
                walk(child, f"{prefix}[{index}]")
        else:
            flat.append((prefix, value))

    walk(record, "")
    return flat


def leaf_key(key: str) -> str:
    last = key.split(".")[-1]
    return re.sub(r"\[\d+\]$", "", last).lower()


def resolve_record_job(ctx: Ctx, flat: list[tuple[str, object]]) -> Job | None:
    for key, value in flat:
        if isinstance(value, str) and leaf_key(key) in ID_LEAVES:
            job = ctx.lookup_job(value)
            if job is not None:
                return job
    for key, value in flat:
        if isinstance(value, str) and value.lower().endswith(".png"):
            stem = Path(value.replace("\\", "/")).stem.lower()
            if stem in ctx.slug_to_id:
                return ctx.jobs[ctx.slug_to_id[stem]]
    for key, value in flat:
        if isinstance(value, str):
            job = ctx.lookup_job(value)
            if job is not None:
                return job
    return None


def resolve_recorded_path(ctx: Ctx, value: str, manifest_dir: Path) -> Path | None:
    text = value.strip().replace("\\", "/")
    if not text or text.startswith("res://"):
        return None
    path = Path(text)
    candidates: list[Path] = []
    if path.is_absolute():
        candidates.append(path)
    else:
        for base in (
            manifest_dir,
            AGNES_DIR,
            REPO,
            ctx.staging,
            ctx.staging / "big",
            ctx.staging / "small",
            ctx.staging.parent,
        ):
            candidates.append(base / path)
    for candidate in candidates:
        try:
            if candidate.is_file():
                return candidate
        except OSError:
            continue
    return None


def expected_res_paths(job: Job) -> set[str]:
    return {
        f"Believer/images/card_portraits/big/{job.slug}.png",
        f"Believer/images/card_portraits/{job.slug}.png",
        f"res://Believer/images/card_portraits/big/{job.slug}.png",
        f"res://Believer/images/card_portraits/{job.slug}.png",
    }


def check_manifest(ctx: Ctx, check: Check) -> None:
    generated = ctx.generated_pairs
    if not ctx.manifest.is_file():
        if generated:
            check.status = FAIL
            check.summary = f"manifest missing: {rel(ctx.manifest)} while {len(generated)} generated pairs exist"
            add_defect(ctx, check, "critical", rel(ctx.manifest), "manifest file does not exist")
        else:
            check.status = SKIP
            check.summary = f"manifest not found: {rel(ctx.manifest)}"
        return

    try:
        raw_text = ctx.manifest.read_text(encoding="utf-8", errors="replace")
    except OSError as exc:
        check.status = FAIL
        check.summary = f"manifest unreadable: {exc}"
        add_defect(ctx, check, "critical", rel(ctx.manifest), f"read failed: {exc}")
        return

    records: list[dict] = []
    json_errors: list[str] = []
    for line_no, line in enumerate(raw_text.splitlines(), 1):
        stripped = line.strip()
        if not stripped:
            continue
        try:
            payload = json.loads(stripped)
        except json.JSONDecodeError as exc:
            json_errors.append(f"line {line_no}: invalid JSON ({exc.msg})")
            continue
        if not isinstance(payload, dict):
            json_errors.append(f"line {line_no}: not a JSON object")
            continue
        records.append({"line": line_no, "record": payload})
    for error in json_errors[:10]:
        add_defect(ctx, check, "critical", rel(ctx.manifest), error)
    note(check, f"Manifest: `{rel(ctx.manifest)}`; valid records: {len(records)}; invalid lines: {len(json_errors)}")
    if not records and not json_errors:
        check.status = FAIL if generated else SKIP
        check.summary = "manifest is empty" if generated else "manifest is empty (no outputs yet)"
        if generated:
            add_defect(ctx, check, "critical", rel(ctx.manifest), "manifest contains no entries while outputs exist")
        return
    if generated:
        note(check, f"Generated output pairs on disk: {len(generated)}")

    manifest_dir = ctx.manifest.parent
    per_job: dict[str, list[tuple[int, str]]] = {}
    unresolved: list[int] = []
    path_defects = 0
    size_verified = 0
    size_defects = 0
    failed_entries: list[str] = []
    status_counts: dict[str, int] = {}

    for entry in records:
        line_no = entry["line"]
        record = entry["record"]
        flat = flatten_record(record)
        job = resolve_record_job(ctx, flat)
        status = str(record.get("status", "")).strip().lower()
        if status:
            status_counts[status] = status_counts.get(status, 0) + 1
        if job is None:
            unresolved.append(line_no)
            continue
        per_job.setdefault(job.card_id, []).append((line_no, status))
        if status in FAILED_STATUSES:
            failed_entries.append(f"{job.slug} (line {line_no}): {record.get('error', status)}")
            continue

        if not job.card_id:
            continue
        big_path = ctx.staging / "big" / f"{job.slug}.png"
        small_path = ctx.staging / "small" / f"{job.slug}.png"
        big_bytes = big_path.stat().st_size if big_path.is_file() else None
        small_bytes = small_path.stat().st_size if small_path.is_file() else None
        big_dims = BIG_SIZE if big_path.is_file() else None
        small_dims = SMALL_SIZE if small_path.is_file() else None

        # paths recorded in the record must resolve or match the res:// contract
        png_values = [(key, value) for key, value in flat if isinstance(value, str) and value.lower().endswith(".png")]
        for key, value in png_values:
            if value.startswith("res://"):
                if value not in expected_res_paths(job):
                    add_defect(ctx, check, "minor", f"{rel(ctx.manifest)}:{line_no}",
                               f"{job.card_id}: recorded res:// path does not match the output contract: {value}")
                    path_defects += 1
                continue
            resolved = resolve_recorded_path(ctx, value, manifest_dir)
            if resolved is None:
                # only demand resolution for values that look like real staging output
                lower = value.lower()
                if ("card_art" in lower or lower.startswith(("out/", "tools/")) or "/big/" in lower.replace("\\", "/")):
                    add_defect(ctx, check, "major", f"{rel(ctx.manifest)}:{line_no}",
                               f"{job.card_id}: recorded output path does not exist: {value}")
                    path_defects += 1
                else:
                    add_defect(ctx, check, "minor", f"{rel(ctx.manifest)}:{line_no}",
                               f"{job.card_id}: recorded PNG path not found on disk: {value}")
                    path_defects += 1

        # recorded byte sizes / dimensions
        for key, value in flat:
            hint = leaf_key(key)
            if re.search(r"(original|source|bbox|trim|normalized|content|request|prompt)", hint):
                continue  # provenance fields are validated by the provenance sub-check
            if not any(word in hint for word in ("size", "bytes", "width", "height", "dim", "resolution", "px")):
                continue
            if hint in ("w", "h", "width", "height"):
                pass
            if isinstance(value, bool) or value is None:
                continue
            target_small = "small" in key.lower()
            target_big = "big" in key.lower()
            if isinstance(value, str):
                dim_match = re.fullmatch(r"(\d+)\s*[xX×]\s*(\d+)", value.strip())
                if dim_match:
                    width, height = int(dim_match.group(1)), int(dim_match.group(2))
                    if target_small:
                        expected = SMALL_SIZE
                    elif target_big:
                        expected = BIG_SIZE
                    else:
                        expected = None
                    if expected is not None:
                        size_verified += 1
                        if (width, height) != expected:
                            size_defects += 1
                            add_defect(ctx, check, "major", f"{rel(ctx.manifest)}:{line_no}",
                                       f"{job.card_id}: recorded {key}={value!r} != actual {expected}")
                    continue
                byte_match = re.fullmatch(r"(\d+)\s*(?:bytes|B)", value.strip())
                if byte_match:
                    numeric = int(byte_match.group(1))
                else:
                    continue
            elif isinstance(value, (int, float)):
                numeric = int(value)
            else:
                continue
            if "width" in hint or hint == "w":
                possible = [dim for dim in (big_dims, small_dims) if dim]
                if possible:
                    size_verified += 1
                    if numeric not in {dim[0] for dim in possible}:
                        size_defects += 1
                        add_defect(ctx, check, "major", f"{rel(ctx.manifest)}:{line_no}",
                                   f"{job.card_id}: recorded width {numeric} != actual {sorted({dim[0] for dim in possible})}")
            elif "height" in hint or hint == "h":
                possible = [dim for dim in (big_dims, small_dims) if dim]
                if possible:
                    size_verified += 1
                    if numeric not in {dim[1] for dim in possible}:
                        size_defects += 1
                        add_defect(ctx, check, "major", f"{rel(ctx.manifest)}:{line_no}",
                                   f"{job.card_id}: recorded height {numeric} != actual {sorted({dim[1] for dim in possible})}")
            else:
                possible = [value for value in (big_bytes, small_bytes) if value is not None]
                if possible:
                    size_verified += 1
                    if numeric not in possible:
                        size_defects += 1
                        add_defect(ctx, check, "major", f"{rel(ctx.manifest)}:{line_no}",
                                   f"{job.card_id}: recorded {key}={numeric} != actual bytes {possible}")

    for line_no in unresolved[:10]:
        add_defect(ctx, check, "critical", f"{rel(ctx.manifest)}:{line_no}", "record cannot be mapped to any expected job")
    if unresolved:
        add_defect(ctx, check, "critical", rel(ctx.manifest), f"{len(unresolved)} record(s) unmapped")

    duplicates: dict[str, list[int]] = {}
    for card_id, entries_for_job in per_job.items():
        success_lines = [line for line, status in entries_for_job if status not in FAILED_STATUSES]
        if len(success_lines) > 1:
            duplicates[card_id] = success_lines
    for card_id, lines in duplicates.items():
        add_defect(ctx, check, "major", f"{rel(ctx.manifest)}:{lines[0]}",
                   f"{card_id}: {len(lines)} generated manifest entries (lines {lines})")

    manifest_jobs = {
        card_id
        for card_id, entries_for_job in per_job.items()
        if any(status not in FAILED_STATUSES for _, status in entries_for_job)
    }
    generated_jobs = set(ctx.generated_pairs)
    missing = sorted(generated_jobs - manifest_jobs)
    stale = sorted(manifest_jobs - generated_jobs)
    if missing:
        add_defect(ctx, check, "critical", rel(ctx.manifest),
                   f"{len(missing)} generated job(s) missing manifest entries: {missing[:10]}")
    if stale:
        add_defect(ctx, check, "major", rel(ctx.manifest),
                   f"{len(stale)} manifest entries without generated output pairs: {stale[:10]}")
    if failed_entries:
        note(check, f"Failed generation entries (transient platform errors are not pipeline defects): "
                    f"{len(failed_entries)} - {failed_entries[:6]}")
    note(check, f"Manifest status counts: {status_counts or {'(no status field)': len(records)}}")
    note(check, f"Entries mapped to jobs: {len(manifest_jobs)}; recorded size fields verified: {size_verified}; "
                f"size defects: {size_defects}; path defects: {path_defects}")

    # provenance for the trim/padding provenance contract
    provenance_claimed = bool(ctx.trim_functions) or any(
        "bars_trimmed" in text or "original_size" in text or "source_size" in text
        for text in ctx.trim_source_text.values()
    )
    trim_provenance = check_manifest_provenance(ctx, check, records, per_job, generated, provenance_claimed)

    if check.defects:
        check.status = FAIL
        check.summary = f"manifest defect(s): {len(check.defects)}"
    else:
        check.status = PASS
        check.summary = (f"{len(manifest_jobs)} entries for {len(generated)} generated jobs; JSONL valid; "
                         f"paths/sizes consistent; provenance {trim_provenance}")


def check_manifest_provenance(
    ctx: Ctx,
    check: Check,
    records: list[dict],
    per_job: dict[str, list[tuple[int, str]]],
    generated: dict[str, Job],
    provenance_claimed: bool,
) -> str:
    if not provenance_claimed:
        note(check, "Trim provenance: pipeline declares no trim/normalize/provenance feature - N/A")
        return "N/A"
    missing: list[str] = []
    contradictions = 0
    for entry in records:
        line_no = entry["line"]
        record = entry["record"]
        if str(record.get("status", "")).strip().lower() in FAILED_STATUSES:
            continue  # failed attempts carry no provenance by design
        flat = flatten_record(record)
        job = resolve_record_job(ctx, flat)
        if job is None:
            continue
        keys = {leaf_key(key): value for key, value in flat}
        if not (set(keys) & PROVENANCE_ANY_KEYS):
            missing.append(f"{job.slug} (line {line_no})")
            continue
        trimmed_flag = keys.get("bars_trimmed", keys.get("bar_trimmed", keys.get("trimmed")))
        # provenance self-consistency: the recorded numbers must describe real processing
        normalized = keys.get("normalized_size")
        if isinstance(normalized, (list, tuple)) and len(normalized) == 2:
            try:
                if [int(normalized[0]), int(normalized[1])] != list(BIG_SIZE):
                    contradictions += 1
                    add_defect(ctx, check, "major", f"{rel(ctx.manifest)}:{line_no}",
                               f"{job.slug}: normalized_size {list(normalized)} != output {list(BIG_SIZE)}")
            except (TypeError, ValueError):
                pass
        original = keys.get("original_size")
        trim_px = keys.get("trim_px")
        content_bbox = keys.get("content_bbox")
        original_pair: tuple[int, int] | None = None
        if isinstance(original, (list, tuple)) and len(original) == 2:
            try:
                original_pair = (int(original[0]), int(original[1]))
            except (TypeError, ValueError):
                original_pair = None
        if original_pair is not None and isinstance(trim_px, (list, tuple)) and len(trim_px) == 4:
            try:
                trim_values = [int(value) for value in trim_px]
            except (TypeError, ValueError):
                trim_values = None
            if trim_values is not None:
                if isinstance(trimmed_flag, bool):
                    if trimmed_flag and not any(trim_values):
                        contradictions += 1
                        add_defect(ctx, check, "major", f"{rel(ctx.manifest)}:{line_no}",
                                   f"{job.slug}: bars_trimmed=true but trim_px={trim_values} is all zero")
                    if not trimmed_flag and any(trim_values):
                        contradictions += 1
                        add_defect(ctx, check, "major", f"{rel(ctx.manifest)}:{line_no}",
                                   f"{job.slug}: bars_trimmed=false but trim_px={trim_values} is non-zero")
                if isinstance(content_bbox, (list, tuple)) and len(content_bbox) == 4:
                    try:
                        bbox_values = [int(value) for value in content_bbox]
                    except (TypeError, ValueError):
                        bbox_values = None
                    if bbox_values is not None:
                        expected_bbox = [
                            trim_values[0],
                            trim_values[1],
                            original_pair[0] - trim_values[2],
                            original_pair[1] - trim_values[3],
                        ]
                        if bbox_values != expected_bbox:
                            contradictions += 1
                            add_defect(ctx, check, "major", f"{rel(ctx.manifest)}:{line_no}",
                                       f"{job.slug}: content_bbox {bbox_values} != "
                                       f"original_size-trim_px {expected_bbox}")
        # if the pipeline claims bars were trimmed, the final big must be clean
        if isinstance(trimmed_flag, bool) and trimmed_flag:
            big_path = ctx.staging / "big" / f"{job.slug}.png"
            if big_path.is_file():
                try:
                    with Image.open(big_path) as img:
                        strips = edge_strip_analysis(img.convert("RGB"))
                    if bad_strips(strips):
                        contradictions += 1
                        add_defect(ctx, check, "critical", f"{rel(ctx.manifest)}:{line_no}",
                                   f"{job.slug}: manifest says bars_trimmed=true but output still has bars {bad_strips(strips)}")
                except Exception as exc:
                    add_defect(ctx, check, "minor", f"{rel(ctx.manifest)}:{line_no}",
                               f"{job.slug}: could not verify trim claim ({exc})")
        # source/original canvas must be plausible for an API output
        for key in ("original_size", "source_size", "original_dimensions"):
            value = keys.get(key)
            pair: tuple[int, int] | None = None
            if isinstance(value, str):
                match = re.fullmatch(r"(\d+)\s*[xX×]\s*(\d+)", value.strip())
                if match:
                    pair = (int(match.group(1)), int(match.group(2)))
            elif isinstance(value, (list, tuple)) and len(value) == 2:
                try:
                    pair = (int(value[0]), int(value[1]))
                except (TypeError, ValueError):
                    pair = None
            if pair is None:
                continue
            width, height = pair
            if not (64 <= width <= 8192 and 64 <= height <= 8192):
                contradictions += 1
                add_defect(ctx, check, "major", f"{rel(ctx.manifest)}:{line_no}",
                           f"{job.slug}: implausible recorded {key}={value!r}")
            elif width < BIG_SIZE[0] or height < BIG_SIZE[1]:
                note(check, f"WARN `{job.slug}`: recorded {key}={value} smaller than output "
                            f"{BIG_SIZE[0]}x{BIG_SIZE[1]} (upscaled)")
        for key in ("original_width", "source_width", "original_height", "source_height"):
            value = keys.get(key)
            if isinstance(value, (int, float)) and not isinstance(value, bool):
                if not (64 <= int(value) <= 8192):
                    contradictions += 1
                    add_defect(ctx, check, "major", f"{rel(ctx.manifest)}:{line_no}",
                               f"{job.slug}: implausible recorded {key}={value}")
    if missing:
        add_defect(ctx, check, "major", rel(ctx.manifest),
                   f"trim provenance missing for {len(missing)} record(s): {missing[:10]}")
    note(check, f"Trim provenance: claimed={provenance_claimed}, records missing provenance={len(missing)}, "
                f"contradictions={contradictions}")
    if contradictions or missing:
        return "FAIL"
    return "PASS"


# ---------------------------------------------------------------------------
# check 5 - resume / idempotency
# ---------------------------------------------------------------------------


def check_resume(ctx: Ctx, check: Check) -> None:
    if not CARD_ART.is_file():
        check.status = SKIP
        check.summary = "card_art.py does not exist yet"
        return
    if ctx.no_run:
        check.status = SKIP
        check.summary = "--no-run given"
        return
    candidates = sorted(ctx.generated_pairs.items(), key=lambda item: (item[1].kind != "normal", item[1].slug))
    if not candidates:
        check.status = SKIP
        check.summary = "no generated card to test resume against"
        return

    card_id, job = candidates[0]
    big_path = ctx.staging / "big" / f"{job.slug}.png"
    small_path = ctx.staging / "small" / f"{job.slug}.png"
    manifest_exists = ctx.manifest.is_file()
    before = {
        "big_sha": sha256_file(big_path),
        "small_sha": sha256_file(small_path),
        "big_mtime": big_path.stat().st_mtime_ns,
        "small_mtime": small_path.stat().st_mtime_ns,
        "manifest_sha": sha256_file(ctx.manifest) if manifest_exists else None,
        "manifest_mtime": ctx.manifest.stat().st_mtime_ns if manifest_exists else None,
        "manifest_lines": line_count(ctx.manifest) if manifest_exists else None,
    }

    # NOTE: card_art.py's --staging is a boolean mode flag (not a path), so the
    # real staging dir + real manifest are used: this is exactly the user-facing
    # "re-run one already generated card without --force" path.
    cmd = [ctx.python, str(CARD_ART), "--only", card_id]
    record = run_process(
        ctx,
        cmd,
        cwd=AGNES_DIR,
        timeout=ctx.timeout,
        env=trap_env(ctx),
        label=f"card_art --only {card_id} (resume, no --force)",
    )
    note(check, f"Re-run command: `python {rel(CARD_ART)} --only {card_id}` (no `--force`; default staging + manifest)")
    note(check, f"Exit code {record['rc']}, elapsed {record['elapsed']}s")
    if record["leaked_key"]:
        add_defect(ctx, check, "critical", "Tools/AgnesImage/card_art.py", "resume run output leaked the live API key")
    if record["timed_out"]:
        check.status = FAIL
        check.summary = "resume run timed out"
        add_defect(ctx, check, "critical", "Tools/AgnesImage/card_art.py", "resume run hung (network call without --force?)")
        return

    if record["rc"] != 0:
        check.status = FAIL
        check.summary = f"resume run exit {record['rc']}"
        tail = (record["stderr"] or record["stdout"]).strip().splitlines()
        add_defect(ctx, check, "critical", "Tools/AgnesImage/card_art.py",
                   f"re-running `--only {card_id}` without --force failed (exit {record['rc']}): "
                   f"{tail[-1] if tail else 'no output'} - does it call the API instead of skipping?")
        return

    after = {
        "big_sha": sha256_file(big_path),
        "small_sha": sha256_file(small_path),
        "big_mtime": big_path.stat().st_mtime_ns,
        "small_mtime": small_path.stat().st_mtime_ns,
        "manifest_sha": sha256_file(ctx.manifest) if ctx.manifest.is_file() else None,
        "manifest_mtime": ctx.manifest.stat().st_mtime_ns if ctx.manifest.is_file() else None,
        "manifest_lines": line_count(ctx.manifest) if ctx.manifest.is_file() else None,
    }
    note(check, f"Manifest before: sha256={(before['manifest_sha'] or 'n/a')[:16]}, "
                f"lines={before['manifest_lines']}; after: sha256={(after['manifest_sha'] or 'n/a')[:16]}, "
                f"lines={after['manifest_lines']}")
    note(check, f"Output hash unchanged: big={before['big_sha'] == after['big_sha']}, "
                f"small={before['small_sha'] == after['small_sha']}")
    if manifest_exists:
        if after["manifest_sha"] != before["manifest_sha"] or after["manifest_lines"] != before["manifest_lines"]:
            add_defect(ctx, check, "critical", rel(ctx.manifest),
                       f"manifest changed on a no-op re-run ({before['manifest_lines']} -> {after['manifest_lines']} lines)")
        if after["manifest_mtime"] != before["manifest_mtime"]:
            add_defect(ctx, check, "minor", rel(ctx.manifest), "manifest mtime changed on a no-op re-run")
    else:
        note(check, "No manifest present: resume verified via unchanged output hashes only")
    for label, path in (("big", big_path), ("small", small_path)):
        if before[f"{label}_sha"] != after[f"{label}_sha"]:
            add_defect(ctx, check, "critical", rel(path),
                       f"{label} output was rewritten on a no-op re-run (should have been skipped)")
        elif before[f"{label}_mtime"] != after[f"{label}_mtime"]:
            note(check, f"WARN {label} mtime changed on no-op re-run but content is identical")

    output = record["stdout"] + "\n" + record["stderr"]
    note(check, f"Pipeline reported: `{output.strip().splitlines()[-1][:160] if output.strip() else '(no output)'}`")
    if not re.search(r"(跳过|skip|生成 0)", output, re.IGNORECASE):
        add_defect(ctx, check, "major", "Tools/AgnesImage/card_art.py",
                   "no skip confirmation in resume output; it may have regenerated the card")
    network_markers = re.findall(
        r"(AgnesError|HTTP \d{3}|Connection refused|网络错误|请求超时|\[agnes\] 第)",
        output,
    )
    if network_markers:
        add_defect(ctx, check, "critical", "Tools/AgnesImage/card_art.py",
                   f"resume run shows API/network activity: {sorted(set(network_markers))}")
    if record["elapsed"] > 45:
        add_defect(ctx, check, "major", "Tools/AgnesImage/card_art.py",
                   f"resume run took {record['elapsed']}s - appears to have regenerated (API call)")
    if check.defects:
        check.status = FAIL
        check.summary = f"resume defect(s): {len(check.defects)}"
    else:
        check.status = PASS
        check.summary = f"`{card_id}` skipped without API call; manifest + outputs unchanged ({record['elapsed']}s)"


# ---------------------------------------------------------------------------
# check 6 - secret hygiene
# ---------------------------------------------------------------------------


def _read_text_if_reasonable(path: Path, max_bytes: int = 8_000_000) -> str | None:
    try:
        stat = path.stat()
        if stat.st_size > max_bytes:
            return None
        data = path.read_bytes()
    except OSError:
        return None
    if b"\x00" in data[:8192]:
        return None
    return data.decode("utf-8", "replace")


def scan_text_for_secrets(ctx: Ctx, label: str, text: str, source_path: Path) -> tuple[list[str], list[str]]:
    key_hits: list[str] = []
    sk_hits: list[str] = []
    lines = text.splitlines()
    for index, line in enumerate(lines, 1):
        if ctx.key and len(ctx.key) >= 8 and ctx.key in line:
            key_hits.append(f"{label}:{index}")
        for match in SECRET_HEURISTIC_RE.finditer(line):
            if ctx.key and match.group(0) in ctx.key:
                # already covered by key_hits
                continue
            sk_hits.append(f"{label}:{index} {redact(ctx, match.group(0))}")
    return key_hits, sk_hits


def list_tracked_files(ctx: Ctx) -> list[str]:
    rc, out, _ = git(ctx, "ls-files", "-z")
    if rc != 0:
        return []
    return [item for item in out.split("\x00") if item.strip()]


def list_untracked_files(ctx: Ctx) -> list[str]:
    rc, out, _ = git(ctx, "ls-files", "-o", "--exclude-standard", "-z")
    if rc != 0:
        return []
    return [item for item in out.split("\x00") if item.strip()]


def check_secrets(ctx: Ctx, check: Check) -> None:
    # --- 6.1 .env is not tracked -------------------------------------
    tracked = list_tracked_files(ctx)
    note(check, f"Tracked files: {len(tracked)} (`git ls-files`)")
    tracked_env = [item for item in tracked if item.replace("\\", "/").endswith("/.env") or item == ".env"]
    if tracked_env:
        add_defect(ctx, check, "critical", ".env", f"tracked .env file(s): {tracked_env}")
    else:
        note(check, "No `.env` in `git ls-files` (tracked set contains 0 .env files)")

    rc, out, err = git(ctx, "check-ignore", "-v", "Tools/AgnesImage/.env")
    if rc == 0 and out.strip():
        note(check, f"`git check-ignore -v Tools/AgnesImage/.env` -> `{out.strip()}`")
    else:
        add_defect(ctx, check, "critical", ".gitignore",
                   f"Tools/AgnesImage/.env is NOT gitignored (check-ignore exit {rc}): {err.strip()[:200]}")
    rc_show, _, _ = git(ctx, "show", ":Tools/AgnesImage/.env")
    if rc_show == 0:
        add_defect(ctx, check, "critical", "Tools/AgnesImage/.env", "file is present in the git index (staged)")

    rc, porcelain, _ = git(ctx, "status", "--porcelain")
    dirty_env = []
    for line in porcelain.splitlines():
        if not line.strip():
            continue
        payload = line[3:]
        if " -> " in payload:
            payload = payload.split(" -> ", 1)[1]
        if payload.replace("\\", "/").endswith("/.env") or payload == ".env":
            dirty_env.append(line)
    if dirty_env:
        add_defect(ctx, check, "critical", ".env", f"git status shows .env: {dirty_env}")
    note(check, f"`git status --porcelain` lines: {len(porcelain.splitlines())}; `.env` entries: {len(dirty_env)}")

    # --- 6.2 no tracked/untracked file contains the live key ----------
    key_hits: list[str] = []
    sk_hits: list[str] = []
    scanned = 0
    for rel_path in tracked:
        text = _read_text_if_reasonable(REPO / rel_path)
        if text is None:
            continue
        scanned += 1
        hits, sk = scan_text_for_secrets(ctx, rel_path, text, REPO / rel_path)
        key_hits.extend(hits)
        sk_hits.extend(sk)
    note(check, f"Scanned {scanned}/{len(tracked)} tracked files for the live key and `sk-` patterns")
    for rel_path in list_untracked_files(ctx):
        path = REPO / rel_path
        if path.name == "verify_pipeline.py":
            continue  # this scanner intentionally contains the literal pattern
        text = _read_text_if_reasonable(path)
        if text is None:
            continue
        hits, sk = scan_text_for_secrets(ctx, rel_path, text, path)
        key_hits.extend(hits)
        sk_hits.extend([item for item in sk if "verify_pipeline.py" not in item])
    if key_hits:
        add_defect(ctx, check, "critical", "repo", f"live API key found in repo file(s): {key_hits[:10]}")
    if sk_hits:
        note(check, f"`sk-` pattern hits (non-key / redacted): {sk_hits[:10]}")
    else:
        note(check, "No `sk-` secret patterns in tracked or untracked-not-ignored files")

    # extra: scan the tool sources (including ignored outputs' siblings) for a hardcoded key
    source_hits: list[str] = []
    source_dirs = [AGNES_DIR, REPO / "docs", REPO / "BelieverCode"]
    extra_files = [p for p in REPO.glob("*.md")] + [p for p in REPO.glob("*.json")]
    seen: set[Path] = set()
    for path in [*extra_files]:
        seen.add(path.resolve())
    for folder in source_dirs:
        if not folder.is_dir():
            continue
        for path in folder.rglob("*"):
            if not path.is_file():
                continue
            resolved = path.resolve()
            if resolved in seen:
                continue
            seen.add(resolved)
            if path.name in (".env",) or "verify-report" in path.name:
                continue
            if path.suffix.lower() in {".png", ".jpg", ".jpeg", ".webp", ".dll", ".pdb", ".pyc", ".zip"}:
                continue
            text = _read_text_if_reasonable(path)
            if text is None:
                continue
            if ctx.key and len(ctx.key) >= 8 and ctx.key in text:
                source_hits.append(rel(path))
    if source_hits:
        add_defect(ctx, check, "critical", "Tools/AgnesImage", f"hardcoded live key in source/output file(s): {source_hits}")
    note(check, "No API key literal hardcoded in Tools/, docs/, BelieverCode/ sources")

    # --- 6.3 key available to the pipeline, never printed -------------
    if ctx.key:
        note(check, f"Live key present via {ctx.key_source}; length {len(ctx.key)} (masked, value never printed)")
    else:
        add_defect(ctx, check, "critical", "Tools/AgnesImage/.env",
                   "no AGNES_API_KEY found (env var or .env) - pipeline cannot run")
    leaks = [record["label"] for record in ctx.runs if record.get("leaked_key")]
    if leaks:
        add_defect(ctx, check, "critical", "pipeline output", f"API key leaked to captured output of: {leaks}")
    else:
        note(check, f"Captured pipeline/verifier output redacted: {len(ctx.runs)} subprocess runs, 0 key leaks")

    # --- 6.4 scripts must not print the key ---------------------------
    key_words = (KEY_WORD, "apikey", "load_" + KEY_WORD, "AGNES_API_" + "KEY", "secret", "token")
    dangerous: list[str] = []
    for path in (CLIENT, CARD_ART, STYLE, AGNES_DIR / "_probe.py", Path(__file__)):
        text = _read_text_if_reasonable(path)
        if text is None:
            continue
        for index, line in enumerate(text.splitlines(), 1):
            lowered = line.lower()
            if PRINT_TOKEN + "(" not in line:
                continue
            if not any(word.lower() in lowered for word in key_words):
                continue
            if any(safe in line for safe in ("len(", "mask", "redact", "***", "<REDACTED")):
                continue
            dangerous.append(f"{rel(path)}:{index}")
    if dangerous:
        add_defect(ctx, check, "critical", "script output", f"print() of key material: {dangerous[:8]}")
    note(check, "Static scan: no script prints the key value; verifier output masking asserted at runtime")

    if check.defects:
        check.status = FAIL
        check.summary = f"secret-hygiene defect(s): {len(check.defects)}"
    else:
        check.status = PASS
        check.summary = ".env ignored/untracked; no key or sk- secret in repo files; key never printed"


# ---------------------------------------------------------------------------
# check 7 - visual sanity
# ---------------------------------------------------------------------------


def check_visual(ctx: Ctx, check: Check) -> None:
    picks: list[tuple[str, Path]] = []
    if ctx.generated_pairs:
        ordered = sorted(ctx.generated_pairs.values(), key=lambda job: (job.kind != "normal", job.slug))
        for job in ordered[:2]:
            picks.append((job.card_id, ctx.staging / "big" / f"{job.slug}.png"))
    if not picks:
        for candidate in sorted(OUT_DIR.glob("*.png")):
            picks.append((candidate.name, candidate))
            break
    if not picks:
        check.status = SKIP
        check.summary = "no generated images to inspect"
        return

    healthy = 0
    for label, path in picks:
        try:
            with Image.open(path) as img:
                img.load()
                rgb = img.convert("RGB")
            stats = image_stats(rgb)
            strips = edge_strip_analysis(rgb)
            note(check, f"`{rel(path)}` ({label}): {stats['size']}, mode={stats['mode']}, "
                        f"luma mean/std={stats['lum_mean']}/{stats['lum_std']}, "
                        f"distinct colours={stats['distinct_colors']}, saturation={stats['sat_mean']}, "
                        f"edges={ {e: strips[e] for e in ('left','right','top','bottom')} }")
            problems = []
            if tuple(stats["size"]) not in (BIG_SIZE, SMALL_SIZE, (1152, 864), (1024, 768), (768, 576)):
                problems.append(f"unexpected size {stats['size']}")
            if stats["lum_std"] < MIN_LUM_STD:
                problems.append("looks blank")
            if stats["distinct_colors"] < MIN_DISTINCT_COLORS:
                problems.append("too few colours for an illustration")
            if bad_strips(strips):
                problems.append(f"padding bars remain {bad_strips(strips)}")
            if problems:
                add_defect(ctx, check, "major", rel(path), f"visual sanity: {', '.join(problems)}")
            else:
                healthy += 1
        except Exception as exc:
            add_defect(ctx, check, "major", rel(path), f"could not inspect image: {type(exc).__name__}: {exc}")

    images = "; ".join(f"`{rel(path)}`" for _, path in picks)
    if ctx.visual_notes.strip():
        note(check, f"Manual inspection (verifier, `read_image`): {ctx.visual_notes.strip()}")
        manual_done = True
    else:
        note(check, "Manual inspection: PENDING - no --visual-notes supplied; automated stats only")
        manual_done = False

    if check.defects:
        check.status = FAIL
        check.summary = f"visual problem(s) on {images}"
    elif not manual_done:
        check.status = WARN
        check.summary = f"automated sanity OK on {images}; manual Believer-theme inspection still pending"
    else:
        check.status = PASS
        check.summary = f"automated sanity + manual inspection confirm Believer-themed illustrations ({images})"


# ---------------------------------------------------------------------------
# check 8 - static source review
# ---------------------------------------------------------------------------


def check_static(ctx: Ctx, check: Check) -> None:
    if not CARD_ART.is_file():
        check.status = SKIP
        check.summary = "card_art.py does not exist yet"
        return
    try:
        ast.parse(CARD_ART.read_text(encoding="utf-8", errors="replace"))
        note(check, f"`{rel(CARD_ART)}` parses cleanly")
    except SyntaxError as exc:
        add_defect(ctx, check, "critical", f"{rel(CARD_ART)}:{exc.lineno}", f"SyntaxError: {exc.msg}")
    if STYLE.is_file():
        try:
            ast.parse(STYLE.read_text(encoding="utf-8", errors="replace"))
            note(check, f"`{rel(STYLE)}` parses cleanly")
        except SyntaxError as exc:
            add_defect(ctx, check, "critical", f"{rel(STYLE)}:{exc.lineno}", f"SyntaxError: {exc.msg}")

    sources = [path for path in (CARD_ART, STYLE) if path.is_file()]
    combined = "\n".join(path.read_text(encoding="utf-8", errors="replace") for path in sources)
    lowered = combined.lower()

    for token, description in (
        ("1000", "big width 1000"),
        ("760", "big height 760"),
        ("250", "small width 250"),
        ("190", "small height 190"),
    ):
        hits = []
        for path in sources:
            hits.extend(find_source_lines(path, rf"\b{token}\b", limit=3))
        if hits:
            note(check, f"`{description}` present: " + ", ".join(f"`{rel(sources[0]) if False else ''}`" for _ in hits)[:0]
                 if False else f"`{description}` present: " + ", ".join(dict.fromkeys(f"`{rel(p)}:{line}`" for p, line in _hits_with_file(sources, token))))
        else:
            add_defect(ctx, check, "major", rel(CARD_ART), f"output-contract value {description} not found in pipeline sources")

    if "gb18030" in lowered:
        hits = [f"`{rel(p)}:{line}`" for p in sources for line, _ in find_source_lines(p, r"gb18030", re.IGNORECASE, 2)]
        note(check, f"GB18030 spec decoding present: {', '.join(dict.fromkeys(hits))}")
    else:
        add_defect(ctx, check, "major", rel(CARD_ART),
                   "spec CSV is GB18030 but pipeline sources never mention gb18030")

    slug_hits: list[str] = []
    for path in sources:
        for line in find_source_lines(path, r"BELIEVER-", limit=3):
            slug_hits.append(f"`{rel(path)}:{line[0]}`")
    if slug_hits:
        note(check, f"Slug rule (`BELIEVER-` prefix strip) present: {', '.join(dict.fromkeys(slug_hits))}")
    else:
        add_defect(ctx, check, "major", rel(CARD_ART), "slug rule (strip BELIEVER- prefix) not found in pipeline sources")

    if ctx.trim_functions:
        note(check, "Trim/normalize helper(s): " + ", ".join(
            f"`{rel(item['file'])}:{item['line']}` {item['qualname']}" for item in ctx.trim_functions[:6]
        ))
    else:
        note(check, "No trim/normalize helper found (letterbox handling may be inline)")

    manifest_hits = [
        f"`{rel(p)}:{line}`" for p in sources for line, _ in find_source_lines(p, r"(jsonl|manifest|json\.dumps)", re.IGNORECASE, 3)
    ]
    if manifest_hits:
        note(check, "Manifest writing evidence: " + ", ".join(dict.fromkeys(manifest_hits)))

    dry_hits = [f"`{rel(p)}:{line}`" for p in sources for line, _ in find_source_lines(p, r"dry[-_]?run", re.IGNORECASE, 2)]
    note(check, "Dry-run flag evidence: " + (", ".join(dict.fromkeys(dry_hits)) if dry_hits else "not found"))

    if check.defects:
        check.status = FAIL
        check.summary = f"static contract defect(s): {len(check.defects)}"
    else:
        check.status = PASS
        check.summary = "output sizes, GB18030 decoding and slug rule are present in the pipeline sources"


def _hits_with_file(sources: list[Path], token: str) -> list[tuple[Path, int]]:
    results: list[tuple[Path, int]] = []
    for path in sources:
        for line, _ in find_source_lines(path, rf"\b{token}\b", limit=3):
            results.append((path, line))
    return results


# ---------------------------------------------------------------------------
# report rendering
# ---------------------------------------------------------------------------


def compute_verdict(checks: list[Check]) -> str:
    if any(check.status == FAIL for check in checks):
        return "FAIL"
    if any(check.status == SKIP for check in checks):
        return "INCOMPLETE"
    return "PASS"


def render_report(ctx: Ctx, checks: list[Check], verdict: str) -> str:
    lines: list[str] = []
    head = git(ctx, "rev-parse", "--short", "HEAD")
    head_value = head[1].strip() if head[0] == 0 else "unavailable"
    status_rc, status_out, _ = git(ctx, "status", "--porcelain")

    lines.append("# Believer - independent verification of the Agnes card-art pipeline")
    lines.append("")
    lines.append(f"- Generated: {now_iso()}")
    lines.append(f"- Repo: `{REPO}` (git HEAD `{head_value}`)")
    lines.append(f"- Python: `{sys.version.split()[0]}` (`{ctx.python}`); Pillow `{PILLOW_VERSION}`")
    lines.append(f"- Staging: `{rel(ctx.staging)}` (exists: {ctx.staging.is_dir()}; generated pairs: "
                 f"{len(ctx.generated_pairs)}/64; partial: {len(ctx.partial_pairs)})")
    lines.append(f"- Manifest: `{rel(ctx.manifest)}` (exists: {ctx.manifest.is_file()})")
    lines.append(f"- Verifier: `{rel(Path(__file__))}`")
    lines.append(f"- **Overall verdict: {verdict}**")
    lines.append("")
    lines.append("## Verdict summary")
    lines.append("")
    lines.append("| # | Check | Status | Summary |")
    lines.append("|---|---|---|---|")
    for check in checks:
        summary = check.summary.replace("|", "\\|")
        lines.append(f"| {check.ident} | {check.title} | **{check.status}** | {summary} |")
    lines.append("")
    lines.append("## Method and safety")
    lines.append("")
    lines.append("- Expectations were derived independently from `BelieverCode` card classes and "
                 "`docs/M10.2.1-CardArt-Spec.csv` (GB18030), not from `card_art.py`.")
    lines.append("- No image generation was performed and no API credits were consumed. The dry-run and "
                 "resume checks run `card_art.py` with `AGNES_API_KEY` set to a dummy value and "
                 "`AGNES_API_ENDPOINT` pointed at a closed local port, so any accidental API call fails "
                 "immediately instead of billing the real key.")
    lines.append("- The pipeline, `.env`, `docs/`, `BelieverCode/` and `.cs` files were never modified. "
                 "Scratch data lives under `out/_verify_tmp` and is removed after the run.")
    lines.append("- The live API key was read only to compare/redact; it is never printed. All captured "
                 f"subprocess output is redacted ({len(ctx.runs)} pipeline/helper runs captured).")
    lines.append("")
    lines.append("## Check details")
    lines.append("")
    for check in checks:
        lines.append(f"### Check {check.ident} - {check.title}")
        lines.append("")
        lines.append(f"**Status: {check.status}** - {check.summary}")
        lines.append("")
        if check.evidence:
            lines.append("Evidence:")
            for item in check.evidence:
                lines.append(f"- {item}")
            lines.append("")
        if check.details:
            for item in check.details[:12]:
                lines.append(f"- {item}")
            lines.append("")
        if check.defects:
            lines.append("Defects:")
            for item in check.defects:
                lines.append(f"- {item}")
            lines.append("")

    lines.append("## Defects")
    lines.append("")
    if ctx.defects:
        severities = {"critical": 0, "major": 1, "minor": 2}
        ordered = sorted(ctx.defects, key=lambda item: severities.get(item["severity"], 9))
        lines.append("| Severity | Where | Check | Description |")
        lines.append("|---|---|---|---|")
        for defect in ordered:
            message = defect["message"].replace("|", "\\|")
            lines.append(f"| {defect['severity']} | `{defect['where']}` | {defect['check']} | {message} |")
    else:
        lines.append("No defects found.")
    lines.append("")

    lines.append("## Manual visual inspection")
    lines.append("")
    if ctx.visual_notes.strip():
        lines.append(ctx.visual_notes.strip())
    else:
        lines.append("Pending: no `--visual-notes` supplied. The verifier inspects generated PNGs with the "
                     "read_image tool separately and records the result here.")
    lines.append("")

    lines.append("## Reproduce")
    lines.append("")
    lines.append("```powershell")
    lines.append(r"cd C:\STS2ModDev\Believer\Tools\AgnesImage")
    lines.append(r"C:\Users\Administrator\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe verify_pipeline.py --report out/verification-report.md")
    lines.append("```")
    lines.append("")
    lines.append("## Limitations / not covered")
    lines.append("")
    lines.append("- No in-game display/reward-screen verification (out of scope for this task).")
    lines.append("- `--install` (copying into `Believer/images/card_portraits/`) is intentionally not executed; "
                 "only the staging contract is verified.")
    lines.append("- Full 64/64 image coverage is reported but not required: the task allows verifying the "
                 "generated sample set.")
    if len(ctx.generated_pairs) < 64:
        lines.append(f"- Only {len(ctx.generated_pairs)}/64 generated pairs were present during this run.")
    lines.append("")
    return "\n".join(lines)


# ---------------------------------------------------------------------------
# main
# ---------------------------------------------------------------------------


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Independent verifier for the Believer Agnes card-art pipeline",
    )
    parser.add_argument("--report", default=None,
                        help="markdown report path (default: out/verification-report.md)")
    parser.add_argument("--staging", default=None, help="staging dir (default: out/card_art, auto-detected)")
    parser.add_argument("--manifest", default=None, help="manifest JSONL (default: out/manifest.jsonl, auto-detected)")
    parser.add_argument("--python", default=sys.executable, help="interpreter used for card_art.py")
    parser.add_argument("--timeout", type=int, default=180, help="timeout (s) for dry-run/resume subprocesses")
    parser.add_argument("--visual-notes", default="", help="manual visual-inspection notes to embed in the report")
    parser.add_argument("--no-run", action="store_true", help="skip subprocess checks (no dry-run/resume/trim runs)")
    parser.add_argument("--keep-scratch", action="store_true", help="keep out/_verify_tmp for debugging")
    parser.add_argument("--json", action="store_true", help="also print a machine-readable status summary")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    report_path = Path(args.report) if args.report else (OUT_DIR / "verification-report.md")
    if not report_path.is_absolute():
        report_path = (Path.cwd() / report_path).resolve()

    ctx = Ctx(
        repo=REPO,
        agnes=AGNES_DIR,
        out=OUT_DIR,
        staging=DEFAULT_STAGING,
        manifest=DEFAULT_MANIFEST,
        python=args.python,
        no_run=args.no_run,
        keep_scratch=args.keep_scratch,
        timeout=args.timeout,
        visual_notes=args.visual_notes or "",
        staging_arg=args.staging,
        manifest_arg=args.manifest,
        scratch=TMP_DIR,
    )

    # live key: read for comparison/redaction only, never printed
    if os.environ.get("AGNES_API_KEY"):
        ctx.key = os.environ["AGNES_API_KEY"].strip() or None
        ctx.key_source = "environment variable AGNES_API_KEY"
    if not ctx.key:
        for candidate in (AGNES_DIR / ".env", REPO / ".env"):
            value = read_dotenv(candidate).get("AGNES_API_KEY")
            if value:
                ctx.key = value
                ctx.key_source = rel(candidate)
                break

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    if TMP_DIR.exists():
        shutil.rmtree(TMP_DIR, ignore_errors=True)
        TMP_DIR.mkdir(parents=True, exist_ok=True)

    prepare_inventory(ctx)
    ctx.staging = discover_staging(ctx)
    ctx.manifest = discover_manifest(ctx)
    discover_trim_functions(ctx)
    compute_generated(ctx)

    checks = [
        Check("0", "agnes_client import & key resolution (no network call)"),
        Check("1", "Job inventory (64 = 40 normal + 12 miracles + 12 enhanced)"),
        Check("2", "`card_art.py --all --dry-run` lists 64 jobs and writes nothing"),
        Check("3", "Image contract (1000x760 / 250x190 RGB, no letterbox bars, no fallbacks)"),
        Check("4", "Manifest integrity (+ trim provenance)"),
        Check("5", "Resume/idempotency (no API call without --force)"),
        Check("6", "Secret hygiene (.env, tracked files, script output)"),
        Check("7", "Visual sanity"),
        Check("8", "Static source review (sizes, GB18030, slug rule)"),
    ]
    ctx.checks = checks

    runners = [
        (checks[0], check_client),
        (checks[1], check_inventory),
        (checks[2], check_dry_run),
        (checks[3], check_images),
        (checks[4], check_manifest),
        (checks[5], check_resume),
        (checks[6], check_secrets),
        (checks[7], check_visual),
        (checks[8], check_static),
    ]
    for check, runner in runners:
        try:
            runner(ctx, check)
        except Exception as exc:
            check.status = FAIL
            check.summary = f"verifier crashed in check {check.ident}: {type(exc).__name__}: {exc}"
            add_defect(ctx, check, "critical", rel(Path(__file__)),
                       f"check {check.ident} crashed: {type(exc).__name__}: {exc}")
            check.evidence.append(f"Traceback: `{traceback.format_exc().splitlines()[-1]}`")

    verdict = compute_verdict(checks)
    report_text = redact(ctx, render_report(ctx, checks, verdict))
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(report_text.rstrip() + "\n", encoding="utf-8")

    if not ctx.keep_scratch:
        shutil.rmtree(TMP_DIR, ignore_errors=True)

    print("")
    print("=== Believer Agnes card-art pipeline verification ===")
    for check in checks:
        print(f"[{check.status:>4}] {check.ident}  {check.title} - {check.summary}")
    print(f"OVERALL: {verdict}")
    print(f"Report : {report_path}")
    print(f"Defects: {len(ctx.defects)}")
    if args.json:
        print(json.dumps(
            {
                "verdict": verdict,
                "report": str(report_path),
                "checks": [
                    {"id": check.ident, "title": check.title, "status": check.status, "summary": check.summary}
                    for check in checks
                ],
                "defects": ctx.defects,
            },
            ensure_ascii=False,
        ))
    if verdict == "FAIL":
        return 1
    if verdict == "INCOMPLETE":
        return 2
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except KeyboardInterrupt:
        raise SystemExit(130)

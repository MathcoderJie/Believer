"""Believer formal reskin atlas.

Reuses the native Silent skeleton and the exact Silent atlas layout (region names, bounds,
offsets, rotate flags, page size, atlas scale) while repainting the cloth/body texture regions
into an ivory-white, gold-trimmed hooded Believer costume.

The native Silent atlas page is used only as the silhouette/alpha source. Every region is painted
individually; no region is deleted and the atlas text is copied verbatim except for the page name.

Inputs (source/reference data, not shipped):
    Assets/Character/Spine/SilentSource/silent_decoded.png
    Assets/Character/Spine/SilentSource/silent_atlas.spatlas

Outputs (shipped in the mod DLL as embedded test resources):
    BelieverCode/ReskinTest/believer_skin.png
    BelieverCode/ReskinTest/believer_skin.atlas
"""

import json
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(r"C:\STS2ModDev\Believer")
SRC = ROOT / "Assets" / "Character" / "Spine" / "SilentSource"
OUT = ROOT / "BelieverCode" / "ReskinTest"
PREVIEW = ROOT / "Assets" / "Character" / "Spine" / "SilentSource"

page = Image.open(SRC / "silent_decoded.png").convert("RGBA")
atlas_json = json.loads((SRC / "silent_atlas.spatlas").read_text(encoding="utf-8", errors="replace"))
atlas_data = atlas_json["atlas_data"]
lines = atlas_data.splitlines()
page_name = lines[0]

PROPS = ("bounds:", "offsets:", "rotate:", "size:", "filter:", "scale:", "pma:", "repeat:")
regions = []
cur = None
for line in lines:
    s = line.strip()
    if not line or line == page_name:
        continue
    if s.startswith(PROPS):
        if cur is not None and s.startswith("bounds:"):
            cur["bounds"] = [int(v) for v in s.split(":", 1)[1].split(",")]
        elif cur is not None and s.startswith("offsets:"):
            cur["offsets"] = s.split(":", 1)[1].strip()
        elif cur is not None and s.startswith("rotate:"):
            cur["rotate"] = int(s.split(":", 1)[1])
    else:
        cur = {"name": line}
        regions.append(cur)

arr = np.array(page, dtype=np.float32)

# ---------------------------------------------------------------- classification
CLOTH = {
    "back arm cloak", "back arm cloak back", "bottom hood", "cape_back_attack", "cape_front_attack",
    "cape_front_blocking", "cloak back", "death_half_scale/death_hood",
    "death_half_scale/death_skirt back", "death_half_scale/death_skirt fabric",
    "death_half_scale/death_torso", "dress", "hood top", "left cape", "right cape",
    "top arm cloak", "torso copy",
}
HOOD_LINING = {"bottom hood"}
WRAP = {
    "back arm", "back arm 1", "back arm 2", "death_half_scale/death_neck",
    "neck bottom", "neck top", "top arm",
}
SKIN = {"back hand", "back hand 2", "shiv_hand", "top hand", "top hand 2"}
DARK = {"eye"}
HIDE = {
    "bottom horn", "top horn", "death_half_scale/death_antlers", "hair", "hair top",
    "death_half_scale/death_hair", "skull", "death_half_scale/death_skull",
    "top foot", "bottom foot",
}
HOOD_VOID = {"head"}
GOLD = {"badge", "death_half_scale/death_spine sash"}
HIDE_WEAPON = {
    "back dagger", "top dagger", "blade_shine", "blade_twirls", "shiv_blur",
}
UNDERROBE = {
    "top leg", "bottom leg bottom", "bottom leg top",
    "death_half_scale/death_bottom leg", "death_half_scale/death_top leg",
}
CUFF = {"top arm", "back arm", "back arm 1", "back arm 2"}
ATTACK_CAPE = {"cape_front_attack", "cape_back_attack"}
KEEP = {"shadow", "slash"}

IVORY_DARK = np.array([0.40, 0.36, 0.29], np.float32)
IVORY_MID = np.array([0.84, 0.80, 0.70], np.float32)
IVORY_LIGHT = np.array([1.00, 0.98, 0.93], np.float32)
GOLD_DARK = np.array([0.42, 0.28, 0.08], np.float32)
GOLD_MID = np.array([0.80, 0.62, 0.24], np.float32)
GOLD_LIGHT = np.array([0.99, 0.90, 0.63], np.float32)
SKIN_DARK = np.array([0.30, 0.26, 0.22], np.float32)
SKIN_MID = np.array([0.55, 0.49, 0.42], np.float32)
SKIN_LIGHT = np.array([0.74, 0.68, 0.60], np.float32)


def tri_map(lum, dark, mid, light):
    lum = np.clip(lum, 0, 1)
    return np.where(lum < 0.5, dark + (mid - dark) * (lum / 0.5),
                    mid + (light - mid) * ((lum - 0.5) / 0.5))


def grain(seed, h, w):
    rng = np.random.default_rng(seed)
    small = rng.normal(0, 1, (max(2, h // 4), max(2, w // 4)))
    small = (small - small.min()) / (np.ptp(small) + 1e-6)
    img = Image.fromarray((small * 255).astype(np.uint8), "L").resize((w, h), Image.BICUBIC)
    return np.array(img, np.float32) / 255.0


def alpha_edges(mask_np, radius=1):
    m = Image.fromarray((mask_np * 255).astype(np.uint8), "L")
    eroded = m.filter(ImageFilter.MinFilter(radius * 2 + 1))
    edge = np.array(m, np.float32) - np.array(eroded, np.float32)
    return np.clip(edge / 255.0, 0, 1)


def paint_hood_void(crop):
    """A clean oval dark hood opening with a thin gold rim (replaces the Silent skull silhouette)."""
    h, w = crop.shape[:2]
    ss = 4
    m = Image.new("L", (w * ss, h * ss), 0)
    d = ImageDraw.Draw(m)
    rx, ry = w * ss * 0.44, h * ss * 0.48
    cx, cy = w * ss / 2, h * ss * 0.52
    d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=255)
    m = m.resize((w, h), Image.LANCZOS)
    mask = np.array(m, np.float32) / 255.0
    yy, xx = np.mgrid[0:h, 0:w]
    dist = np.sqrt(((xx - w / 2) / (w * 0.5)) ** 2 + ((yy - h * 0.52) / (h * 0.5)) ** 2)
    base = 0.04 + 0.08 * np.clip((yy / max(1, h)) * 0.8 + dist * 0.3, 0, 1)
    rgb = np.stack([base * 0.95, base * 0.86, base * 0.72], axis=2)
    out = crop.copy()
    out[..., :3] = out[..., :3] * (1 - mask[..., None]) + rgb * 255.0 * mask[..., None]
    rim = np.clip(alpha_edges(mask, 2) * 1.6, 0, 1)
    gold = np.array([GOLD_MID[0], GOLD_MID[1], GOLD_MID[2]], np.float32) * 255.0
    out[..., :3] = out[..., :3] * (1 - rim[..., None]) + gold * rim[..., None]
    out[..., 3] = np.maximum(crop[..., 3], (mask * 255.0).astype(np.float32))
    return out


def paint_region(name, crop, kind):
    h, w = crop.shape[:2]
    rgba = crop.copy()
    rgb = rgba[..., :3] / 255.0
    alpha = rgba[..., 3:4] / 255.0
    lum = np.clip(0.299 * rgb[..., 0:1] + 0.587 * rgb[..., 1:2] + 0.114 * rgb[..., 2:3], 0, 1)
    g = grain(abs(hash(name)) % 100000, h, w)[..., None]

    if kind == "cloth":
        base = tri_map(lum, IVORY_DARK, IVORY_MID, IVORY_LIGHT)
        # large cloth folds + fine grain
        yy = np.linspace(0, 1, h, dtype=np.float32)[:, None, None]
        xx = np.linspace(0, 1, w, dtype=np.float32)[None, :, None]
        fold = (np.sin((xx * 5.0 + yy * 2.0) * math.pi * 2) * 0.5 + 0.5)
        base *= (0.92 + 0.12 * fold)
        base += (g - 0.5) * 0.08
        # warm shadow at the bottom
        base *= (1.0 - 0.12 * yy)
    elif kind == "hood_lining":
        base = tri_map(lum, np.array([0.04, 0.035, 0.03], np.float32),
                       np.array([0.10, 0.09, 0.08], np.float32),
                       np.array([0.20, 0.18, 0.16], np.float32))
    elif kind == "wrap":
        # smooth away the original bandage stripes, then paint soft ivory sleeve cloth
        lum_img = Image.fromarray((lum[..., 0] * 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(2.2))
        smooth = np.array(lum_img, np.float32)[..., None] / 255.0
        base = tri_map(smooth, np.array([0.40, 0.36, 0.30], np.float32),
                       np.array([0.78, 0.74, 0.66], np.float32),
                       np.array([0.97, 0.95, 0.90], np.float32))
        yy = np.linspace(0, 1, h, dtype=np.float32)[:, None, None]
        base *= (1.0 - 0.10 * yy)
        base += (g - 0.5) * 0.05
    elif kind == "underrobe":
        # dark grey cloth interior so leg/foot silhouettes read as shadow inside the robe
        base = tri_map(lum, np.array([0.18, 0.16, 0.14], np.float32),
                       np.array([0.30, 0.28, 0.24], np.float32),
                       np.array([0.44, 0.41, 0.36], np.float32))
        base += (g - 0.5) * 0.04
    elif kind == "skin":
        base = tri_map(lum, SKIN_DARK, SKIN_MID, SKIN_LIGHT)
    elif kind == "dark":
        base = tri_map(lum, np.array([0.03, 0.025, 0.02], np.float32),
                       np.array([0.08, 0.07, 0.06], np.float32),
                       np.array([0.16, 0.14, 0.12], np.float32))
    elif kind == "gold":
        base = tri_map(lum, GOLD_DARK, GOLD_MID, GOLD_LIGHT)
    elif kind == "slash":
        base = tri_map(lum, np.array([0.55, 0.42, 0.16], np.float32),
                       np.array([0.95, 0.82, 0.48], np.float32),
                       np.array([1.0, 0.98, 0.88], np.float32))
    else:
        base = rgb

    # soft inner rim light for cloth, gold edge for trimmed pieces
    edge = alpha_edges(alpha[..., 0])
    if kind in ("cloth", "wrap", "hood_lining"):
        base = base * (1.0 - 0.45 * edge[..., None]) + IVORY_LIGHT[None, None, :] * 0.45 * edge[..., None]
    if name in CLOTH and name not in HOOD_LINING and kind == "cloth":
        # 2px gold border along the attachment silhouette
        gold_edge = alpha_edges(alpha[..., 0], 2)
        base = base * (1.0 - 0.85 * gold_edge[..., None]) + GOLD_MID[None, None, :] * 0.85 * gold_edge[..., None]

    rgba[..., :3] = np.clip(base, 0, 1) * 255.0
    return rgba


def add_cuff(rgba):
    """Gold cuffs at both ends of a sleeve strip (kept subtle, no bone/mesh changes)."""
    h, w = rgba.shape[:2]
    ss = 3
    mask = Image.new("L", (w * ss, h * ss), 0)
    d = ImageDraw.Draw(mask)
    horizontal = w >= h
    band = max(2 * ss, int((w if horizontal else h) * ss * 0.09))
    if horizontal:
        d.rectangle([2 * ss, 0, band, h * ss], fill=255)
        d.rectangle([w * ss - band - 2 * ss, 0, w * ss - 2 * ss, h * ss], fill=255)
    else:
        d.rectangle([0, 2 * ss, w * ss, band], fill=255)
        d.rectangle([0, h * ss - band - 2 * ss, w * ss, h * ss - 2 * ss], fill=255)
    mask = mask.resize((w, h), Image.LANCZOS)
    m = np.array(mask, np.float32) / 255.0
    m *= (rgba[..., 3] / 255.0)
    m = m[..., None]
    gold = np.array(GOLD_MID, np.float32) * 255.0
    out = rgba.copy()
    out[..., :3] = out[..., :3] * (1 - m) + gold * m
    return out


def add_belt(rgba, rotated):
    """Draw a narrow gold belt band across the displayed torso region."""
    base = np.clip(rgba, 0, 255).astype(np.uint8)
    disp = base
    if rotated:
        disp = np.array(Image.fromarray(base, "RGBA").rotate(-90, expand=True))
    h, w = disp.shape[:2]
    ss = 4
    band = max(3, int(h * 0.055))
    canvas = Image.new("RGBA", (w * ss, h * ss), (0, 0, 0, 0))
    d = ImageDraw.Draw(canvas)
    y = int(h * 0.66 * ss)
    gold = (int(GOLD_MID[0] * 255), int(GOLD_MID[1] * 255), int(GOLD_MID[2] * 255), 255)
    dark = (int(GOLD_DARK[0] * 255), int(GOLD_DARK[1] * 255), int(GOLD_DARK[2] * 255), 255)
    light = (int(GOLD_LIGHT[0] * 255), int(GOLD_LIGHT[1] * 255), int(GOLD_LIGHT[2] * 255), 255)
    d.rectangle([0, y, w * ss, y + band * ss], fill=gold)
    d.rectangle([0, y + band * ss, w * ss, y + int(band * 1.5) * ss], fill=dark)
    d.rectangle([0, y - int(band * 0.45) * ss, w * ss, y], fill=light)
    # simple center buckle
    cx = w * ss / 2
    r = band * ss * 0.9
    d.ellipse([cx - r, y - r * 0.1, cx + r, y + band * ss + r * 0.1], fill=gold)
    d.ellipse([cx - r * 0.55, y + band * ss * 0.2, cx + r * 0.55, y + band * ss * 0.8], fill=light)
    belt = np.array(canvas.resize((w, h), Image.LANCZOS), np.float32)
    m = (belt[..., 3:4] / 255.0) * (disp[..., 3:4] / 255.0)
    out = disp.copy()
    out[..., :3] = out[..., :3] * (1 - m) + belt[..., :3] * m
    if rotated:
        out = np.array(Image.fromarray(out.astype(np.uint8), "RGBA").rotate(-90, expand=True))
        out = out[:rgba.shape[0], :rgba.shape[1]]
    return out.astype(np.uint8)


def draw_seal(rgba, radius_scale=0.52):
    """Draw a symmetrical gold seal (ring + disc + rays) in the middle of a torso region."""
    h, w = rgba.shape[:2]
    ss = 4
    canvas = Image.new("RGBA", (w * ss, h * ss), (0, 0, 0, 0))
    d = ImageDraw.Draw(canvas)
    cx, cy = w * ss / 2, h * ss / 2
    r = min(w, h) * ss * radius_scale
    gold = (int(GOLD_MID[0] * 255), int(GOLD_MID[1] * 255), int(GOLD_MID[2] * 255), 255)
    light = (int(GOLD_LIGHT[0] * 255), int(GOLD_LIGHT[1] * 255), int(GOLD_LIGHT[2] * 255), 255)
    dark = (int(GOLD_DARK[0] * 255), int(GOLD_DARK[1] * 255), int(GOLD_DARK[2] * 255), 255)
    d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=gold, width=max(1, int(0.075 * r)))
    d.ellipse([cx - r * 0.72, cy - r * 0.72, cx + r * 0.72, cy + r * 0.72], outline=dark,
              width=max(1, int(0.035 * r)))
    d.ellipse([cx - r * 0.30, cy - r * 0.30, cx + r * 0.30, cy + r * 0.30], fill=gold)
    d.ellipse([cx - r * 0.18, cy - r * 0.18, cx + r * 0.18, cy + r * 0.18], fill=light)
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        d.line([cx + dx * r * 0.95, cy + dy * r * 0.95, cx + dx * r * 1.45, cy + dy * r * 1.45],
               fill=gold, width=max(1, int(0.08 * r)))
    seal = canvas.resize((w, h), Image.LANCZOS)
    seal_arr = np.array(seal, dtype=np.float32)
    mask = seal_arr[..., 3:4] / 255.0
    out = rgba.copy()
    out[..., :3] = out[..., :3] * (1 - mask) + seal_arr[..., :3] * mask
    return out


# ---------------------------------------------------------------- apply
for region in regions:
    name = region["name"]
    if "bounds" not in region:
        continue
    x, y, w, h = region["bounds"]
    crop = arr[y:y + h, x:x + w].copy()
    if name in HIDE:
        crop[..., 3] = 0
    elif name in HIDE_WEAPON:
        crop[..., 3] = 0
    elif name in DARK:
        crop = paint_region(name, crop, "dark")
    elif name in HOOD_VOID:
        crop = paint_hood_void(crop)
    elif name in GOLD:
        crop = paint_region(name, crop, "gold")
    elif name in SKIN:
        crop = paint_region(name, crop, "skin")
    elif name in HOOD_LINING:
        crop = paint_region(name, crop, "hood_lining")
    elif name in CLOTH:
        crop = paint_region(name, crop, "cloth")
        if name in ("torso copy", "death_half_scale/death_torso"):
            crop = draw_seal(crop)
            if name == "torso copy":
                crop = add_belt(crop, region.get("rotate") == 90)
    elif name in WRAP:
        crop = paint_region(name, crop, "wrap")
    elif name in UNDERROBE:
        crop = paint_region(name, crop, "underrobe")
    elif name == "slash":
        crop = paint_region(name, crop, "slash")
    # shadow and anything unknown: keep original

    if name in CUFF:
        crop = add_cuff(crop)
    if name in ATTACK_CAPE:
        # The native attack cape is deliberately huge; soften its opacity so the body stays readable
        crop[..., 3] = (crop[..., 3].astype(np.float32) * 0.28).astype(np.uint8)

    arr[y:y + h, x:x + w] = crop

# Any pixels outside every atlas region are padding leftovers; some mesh UVs may sample them.
# Make them transparent so original Silent leftovers (antlers/green cloth) cannot leak into the skin.
coverage = np.zeros((page.height, page.width), dtype=bool)
for region in regions:
    if "bounds" not in region:
        continue
    bx, by, bw, bh = region["bounds"]
    coverage[max(0, by):min(page.height, by + bh), max(0, bx):min(page.width, bx + bw)] = True
outside = ~coverage
arr[outside, 3] = 0

out = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")
OUT.mkdir(parents=True, exist_ok=True)
out.save(OUT / "believer_skin.png")
# preserve the complete atlas text, only change the page filename
new_lines = [("believer_skin.png" if line == page_name else line) for line in lines]
(OUT / "believer_skin.atlas").write_text("\n".join(new_lines) + "\n", encoding="utf-8")
print("written", OUT / "believer_skin.png", out.size)
print("regions", len(regions), "page", page.size)

from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image, ImageOps

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from agnes_client import generate_to_file  # noqa: E402

ROOT = HERE.parents[1]
RAW = ROOT / "Assets" / "CardArt" / "Generated" / "Character" / "_source"
SOURCE = ROOT / "Assets" / "CardArt" / "Generated" / "Character"
GAME_CHAR = ROOT / "Believer" / "images" / "charui"
GAME_SELECT = ROOT / "Believer" / "images" / "character" / "select"

STYLE = (
    "Dark gothic fantasy, painterly Slay the Spire 2 character-select illustration, "
    "mysterious hooded Believer, face completely hidden inside deep black hood shadow, "
    "ivory-white robes with antique-gold embroidery and circular chest seal, "
    "white-gold divine radiance, solemn and majestic, rich hand-painted brushwork, "
    "dark desaturated cathedral background, no text, no letters, no UI, no border, no logo, no watermark."
)

PROMPTS = {
    "believer_select": (
        "Wide 16:9 character-select background, three-quarter full-body hooded Believer standing "
        "slightly left of center, frontal but subtly turned, ivory-white robe with gold trim, "
        "deep black faceless hood, circular gold chest seal, layered cloak and wide sleeves, "
        "calm imposing presence, soft white-gold halo behind the head, dark cathedral niche and "
        "candlelit stone background, large empty dark area on the right for the character-select "
        "info panel, dramatic but readable, no text, no UI, no card frame. " + STYLE
    ),
    "believer_select_icon": (
        "Vertical character-select button icon, waist-up hooded Believer, centered, "
        "ivory-white and antique-gold robes, faceless black hood, circular gold chest seal, "
        "quiet white-gold rim light, dark simple background, strong silhouette readable at small size. "
        + STYLE
    ),
    "believer_ui_icon": (
        "Square UI avatar, chest-up hooded Believer portrait, centered, face completely hidden in black "
        "hood shadow, ivory-white hood and gold trim, circular gold chest seal, white-gold rim light, "
        "very simple dark background, bold readable silhouette at 128 pixels, no extra objects. " + STYLE
    ),
    "believer_map_marker": (
        "Square map marker icon, stylized hooded Believer head and shoulders, centered, "
        "ivory-white hood with antique-gold trim, deep black faceless shadow, small gold chest seal, "
        "simple dark radial background, strong clean silhouette readable at 32 pixels, no text. " + STYLE
    ),
    "omniscience_card": (
        "Epic card illustration, the instant the Omniscience form activates: the hooded Believer "
        "levitating at the center, ivory-white robes and cloak blown upward by divine force, "
        "seven concentric golden rings around the body, seven small white-gold divinity orbs orbiting, "
        "brilliant platinum-white holy light bursting from the chest and eyes hidden in shadow, "
        "a huge sacred seal unfolding on the ground below, dark cathedral void, absolute divine "
        "invincibility and transcendence, moment just before the explosion, single clear subject, "
        "no text, no card frame, no UI, no letters. " + STYLE
    ),
}


def fit_square(src: Path, dst: Path, size: tuple[int, int]) -> None:
    image = Image.open(src).convert("RGB")
    image = ImageOps.fit(image, size, method=Image.Resampling.LANCZOS, centering=(0.5, 0.5))
    dst.parent.mkdir(parents=True, exist_ok=True)
    image.save(dst, format="PNG", optimize=True)
    print(f"{dst.relative_to(ROOT)} {image.size}")


def fit_cover(src: Path, dst: Path, size: tuple[int, int], centering=(0.5, 0.5)) -> None:
    image = Image.open(src).convert("RGB")
    image = ImageOps.fit(image, size, method=Image.Resampling.LANCZOS, centering=centering)
    dst.parent.mkdir(parents=True, exist_ok=True)
    image.save(dst, format="PNG", optimize=True)
    print(f"{dst.relative_to(ROOT)} {image.size}")


def main() -> int:
    RAW.mkdir(parents=True, exist_ok=True)
    SOURCE.mkdir(parents=True, exist_ok=True)
    GAME_SELECT.mkdir(parents=True, exist_ok=True)
    GAME_CHAR.mkdir(parents=True, exist_ok=True)

    # 1. Omniscience card: preserve the raw generated image, then export 1000x760 / 250x190.
    omni_raw = RAW / "omniscience_card.png"
    if not omni_raw.exists():
        print("Generating omniscience_card ...")
        generate_to_file(PROMPTS["omniscience_card"], omni_raw, size="1024x768")
    fit_cover(omni_raw, SOURCE / "omniscience_card.png", (1000, 760))
    fit_square(omni_raw, ROOT / "Believer/images/card_portraits/big/omniscience_card.png", (1000, 760))
    fit_square(omni_raw, ROOT / "Believer/images/card_portraits/omniscience_card.png", (250, 190))

    # 2. Character select wide background.
    bg_raw = RAW / "believer_select.png"
    if not bg_raw.exists():
        print("Generating believer_select ...")
        generate_to_file(PROMPTS["believer_select"], bg_raw, size="1536x864")
    fit_cover(bg_raw, SOURCE / "believer_select.png", (1920, 1080), (0.42, 0.5))
    fit_cover(bg_raw, GAME_SELECT / "believer_select.png", (1920, 1080), (0.42, 0.5))

    # 3. Character-select button icon; 132x195 is the existing project contract.
    sel_raw = RAW / "believer_select_icon.png"
    if not sel_raw.exists():
        print("Generating believer_select_icon ...")
        generate_to_file(PROMPTS["believer_select_icon"], sel_raw, size="512x768")
    fit_square(sel_raw, SOURCE / "believer_select_icon.png", (132, 195))
    fit_square(sel_raw, GAME_CHAR / "char_select_char_name.png", (132, 195))

    # 4. In-run UI icon / loadout avatar; 128x128 is the existing project contract.
    ui_raw = RAW / "believer_ui_icon.png"
    if not ui_raw.exists():
        print("Generating believer_ui_icon ...")
        generate_to_file(PROMPTS["believer_ui_icon"], ui_raw, size="512x512")
    fit_square(ui_raw, SOURCE / "believer_ui_icon.png", (128, 128))
    fit_square(ui_raw, GAME_CHAR / "character_icon_char_name.png", (128, 128))

    # 5. Map marker; 128x128 is the existing project contract.
    map_raw = RAW / "believer_map_marker.png"
    if not map_raw.exists():
        print("Generating believer_map_marker ...")
        generate_to_file(PROMPTS["believer_map_marker"], map_raw, size="512x512")
    fit_square(map_raw, SOURCE / "believer_map_marker.png", (128, 128))
    fit_square(map_raw, GAME_CHAR / "map_marker_char_name.png", (128, 128))

    # 6. Locked select icon: dark desaturated variant of the same portrait, not a new character design.
    locked = Image.open(sel_raw).convert("RGB")
    locked = ImageOps.grayscale(locked).convert("RGB")
    locked = ImageOps.fit(locked, (132, 195), method=Image.Resampling.LANCZOS)
    locked = Image.blend(locked, Image.new("RGB", locked.size, (18, 18, 22)), 0.35)
    locked.save(GAME_CHAR / "char_select_char_name_locked.png", format="PNG", optimize=True)
    print("Believer/images/charui/char_select_char_name_locked.png")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

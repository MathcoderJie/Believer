"""Believer 卡图统一画风与提示词构建模块。

本模块只做两件事：

1. 定义全部卡图共用的画风常量（黑暗哥特教堂奇幻 + 信徒形象 + 负面约束）；
2. 把 ``docs/M10.2.1-CardArt-Spec.csv`` 每一行的 ``recommended_visual`` /
   ``composition`` 转成英文提示词。

画风基线（来自 M10.2 审计与 M10.2.1 清单）：

- 黑暗哥特教堂奇幻，绘画感强，接近 Slay the Spire 2 的卡图插画；
- 信徒（Believer）是视觉中心：兜帽、象牙白袍、金边装饰、面部藏在阴影里；
- 神圣金色光效、强明暗对比、体积光；
- 构图居中，效果主体占前景，画面里不放文字、费用、稀有度边框。

强化神迹（``variant="enhanced"``）不改变主体与构图，只叠加强烈白金光、
金色神环和升腾光幕，保证与普通神迹“同主题、不同画面强度”。

提示词全部为英文；若某张卡没有英文场景翻译（例如后续新增卡牌），
回退使用规格表中的原始中文推荐画面，保证脚本仍可用。

用法::

    from believer_style import build_prompt
    prompt = build_prompt(row, variant="normal")   # row 为 CSV 一行（dict）
    prompt = build_prompt(row, variant="enhanced")  # row 传对应普通神迹行
"""

from __future__ import annotations

from collections.abc import Mapping
from typing import Literal

# 提示词版本；写入 manifest，便于日后回溯“这批图是用哪版提示词生成的”。
PROMPT_VERSION = "m10.2.1-v1"

Variant = Literal["normal", "miracle", "enhanced"]
VARIANTS: tuple[str, ...] = ("normal", "miracle", "enhanced")

# ---------------------------------------------------------------------------
# 1. 全局画风常量
# ---------------------------------------------------------------------------

# 画风：黑暗哥特教堂、绘画感卡图、神圣金色光、体积光。
STYLE_PREFIX = (
    "Dark gothic cathedral fantasy, painterly Slay the Spire 2 card illustration, "
    "dramatic divine golden light breaking through deep shadow, volumetric god rays, "
    "aged stone and candlelit atmosphere, rich painterly brushwork, "
    "muted desaturated darks contrasted with luminous ivory-white and antique-gold accents"
)

# 主角形象：兜帽信徒 + 象牙白袍 + 金边 + 面部阴影。
CHARACTER = (
    "The Believer: a hooded figure in a flowing ivory-white robe with ornate gold trim, "
    "face hidden in deep shadow, calm and devout presence, hands faintly glowing with holy light"
)

# 满幅构图要求（Agnes 偶尔会返回白/灰留白条，必须在提示词里明确禁止）。
FULL_BLEED = (
    "full-bleed composition filling the entire frame edge to edge, wide landscape framing, "
    "no letterbox bars, no white margins, no padding"
)

# 负面约束：不出现任何文字、卡框、UI、水印。
NEGATIVE = (
    "No text, no letters, no numbers, no card frame, no border, no UI elements, "
    "no cost icon, no rarity gem, no watermark, no signature, no logo"
)

# 统一追加在每条提示词末尾的风格后缀。
STYLE_SUFFIX = f"{FULL_BLEED}. {NEGATIVE}."

# 强化神迹：与普通神迹同一主体/构图，只增强光效。
ENHANCED_RADIANCE = (
    "Enhanced miracle variant: keep exactly the same subject, pose and composition as the normal "
    "miracle, but amplify it with an overwhelming white-gold radiance, a great golden halo behind "
    "the Believer, and an ascending veil of light; it must be clearly distinct from the normal "
    "miracle card while preserving the same thematic silhouette"
)

# 普通神迹：史诗构图，但 12 张不能画成同一站姿。
MIRACLE_EMPHASIS = (
    "Epic miracle card illustration: the specific theme must be instantly recognizable, "
    "and no two miracles may share the same standing pose"
)

# 每种变体的构图约束（对应 CSV 的 composition 列）。
COMPOSITION: dict[str, str] = {
    "normal": (
        "the Believer is the visual focal point with hood, ivory-white robe and gold trim preserved; "
        "the card's effect occupies the foreground; no text, cost gem or rarity frame inside the "
        "illustration; strong central subject with calm negative space near the top and bottom so the "
        "image can be crop-fitted to a 1000x760 card portrait"
    ),
    "miracle": (
        "epic miracle composition; the specific theme must be clearly recognizable; do not render all "
        "twelve miracles in the same standing pose; keep safe margins for the card frame, with a strong "
        "central subject and dramatic vertical or diagonal divine light"
    ),
    "enhanced": (
        "standalone enhanced-card composition; intensified light concentrated around the outer edge of "
        "the subject and never covering the card text area; it must not share the same image as the "
        "normal miracle card"
    ),
}

# ---------------------------------------------------------------------------
# 2. 逐卡英文场景描述（由 CSV recommended_visual 翻译，按 card_id 索引）
# ---------------------------------------------------------------------------

VISUAL_EN: dict[str, str] = {
    # ---- 初始卡（3）----
    "BELIEVER-STRIKE_BELIEVER": (
        "the Believer pivots sideways and swings a blazing blade of light at a single target, "
        "a golden impact burst on the right side of the frame"
    ),
    "BELIEVER-DEFEND_BELIEVER": (
        "a translucent holy shield of light forms before the ivory robe, edged in gold, "
        "the figure standing firm with a steady stance"
    ),
    "BELIEVER-FOCUS": (
        "eyes closed in meditation, a small orb of white-gold light gathering at the chest and palm, "
        "faint sacred light breathing in the background"
    ),
    # ---- 白卡（21）----
    "BELIEVER-HEAVY_STRIKE": (
        "a two-handed charged heavy blow, a golden shockwave and shattered motes of light covering "
        "the target, weight emphasized over speed"
    ),
    "BELIEVER-TRIPLE_SLASH": (
        "three consecutive arcs of golden light slash the same target, lingering trails fanning out "
        "in a fan shape"
    ),
    "BELIEVER-SWEEP": (
        "a low horizontal sweep, a band of white-gold light cutting across all enemies in the "
        "foreground, emphasizing a wide lateral composition"
    ),
    "BELIEVER-STEADFAST": (
        "wide robe sleeves crossed in front as layered gold-edged shields of light, a third layer of "
        "light patterns surfacing behind them"
    ),
    "BELIEVER-SILENT_PRAYER": (
        "head bowed in silent prayer, the edge of the figure dissolving into golden light dust, "
        "suggesting the card is consumed"
    ),
    "BELIEVER-FAITH_OFFERING": (
        "both hands lifting a cluster of white-gold holy flame, its firelight illuminating the "
        "shadowed face"
    ),
    "BELIEVER-INNER_CALM": (
        "a still breathing pose, motes of divinity and a soft shield unfolding around the body at the "
        "same time"
    ),
    "BELIEVER-DEDICATION": (
        "both hands pressed to a holy emblem on the chest, the golden emblem spreading a heavy curtain "
        "of protective light"
    ),
    "BELIEVER-PURIFICATION": (
        "white light rising from the feet and washing away dark mist, forming clean concentric ripples "
        "and a shield"
    ),
    "BELIEVER-PREPARATION": (
        "one hand flipping a glowing page, one card drawn into the foreground while another falls into "
        "shadow, showing draw then discard"
    ),
    "BELIEVER-PRAYER": (
        "a praying gesture with an unfurled ribbon of scripture light, its end turning into two "
        "floating cards"
    ),
    "BELIEVER-DIVINE_GRACE": (
        "a column of divine grace descending onto the shoulders and forming a golden shield, dark "
        "cathedral shadows kept in the background"
    ),
    "BELIEVER-COMPOSURE": (
        "a low, grounded stance, an empty divinity slot at the chest holding a faint light core, "
        "expressing zero divinity"
    ),
    "BELIEVER-ARMOR_BREAK": (
        "a golden fist shattering the target's armor plates, shards flying outward, the target clearly "
        "wearing armor"
    ),
    "BELIEVER-PURSUIT": (
        "the afterimage of the previous strike has not yet faded as a second pursuing light blade cuts "
        "in from the side and rear"
    ),
    "BELIEVER-PIERCE": (
        "a slender golden beam of holy light piercing straight through the target's outline, minimal "
        "impact lines in the background"
    ),
    "BELIEVER-SEVERING_STRIKE": (
        "a diagonal slash severing the target's threads of life light, a broken halo around a "
        "half-health target"
    ),
    "BELIEVER-BEACON": (
        "a beacon pillar of light planted behind or hovering nearby, a golden protective ring lighting "
        "up as divinity is gained"
    ),
    "BELIEVER-GUARDIAN": (
        "huge guardian wings of light and a circular holy seal unfolding behind the Believer, residual "
        "divinity motes gathering at the bottom"
    ),
    "BELIEVER-BESTOW": (
        "two streams of light rain poured from the sleeve onto the ground, each landing point "
        "generating a small shield sigil"
    ),
    "BELIEVER-CONCENTRATION": (
        "a floating sigil of concentration, a skill card's light trail passing through it and "
        "converting into motes of divinity"
    ),
    # ---- 蓝卡（16）----
    "BELIEVER-DIVINE_PUNISHMENT": (
        "a pillar of lightning striking the main target first, then splitting into golden arcs "
        "spreading across the whole battlefield"
    ),
    "BELIEVER-SACRED_JUDGMENT": (
        "a high holy judgment sword descending, a second sword shadow splitting from the clouds as "
        "divinity overflows"
    ),
    "BELIEVER-VERDICT": (
        "scales of verdict hovering above the target, its negative statuses turned into chains and "
        "sin-marks weighing down one side"
    ),
    "BELIEVER-CHAIN": (
        "four holy chains lashing the same target in sequence, forming a continuous rhythm of golden "
        "afterimages"
    ),
    "BELIEVER-FINALITY": (
        "a massive finishing slash covering the foreground, a broken clock face of the end appearing "
        "behind the half-health target"
    ),
    "BELIEVER-FIRE_OFFERING": (
        "a burning offering flame held high, its core white-gold rather than pure red, the edge of the "
        "card turning to ash"
    ),
    "BELIEVER-PRICE": (
        "two glowing pages floating above an open palm while golden energy drips from the other side, "
        "expressing price and exchange"
    ),
    "BELIEVER-SACRIFICE": (
        "the hem of the robe turning into sacrificial flame, a first white wave followed by a second "
        "gold-red fire rising from the ground"
    ),
    "BELIEVER-ABSOLUTION": (
        "both hands crossing to release dark chains from the body, a white absolution halo and a "
        "shield unfolding together"
    ),
    "BELIEVER-PROPHECY": (
        "a star chart and prophetic wheel of light unfolding, three glowing pages drawn out and one "
        "falling into darkness, ending in motes of divinity"
    ),
    "BELIEVER-ECHO": (
        "the light trail of an attack card forming a translucent echoing outline in the background, "
        "a second impact landing slightly later"
    ),
    "BELIEVER-LIMIT_BREAK": (
        "energy slots and halos broken open by golden cracks, glowing pages flying out of the "
        "fissures as divinity overflows"
    ),
    "BELIEVER-THEOPHANY": (
        "a theophany pillar of light enveloping the Believer, surrounded by motes of divinity, a "
        "shield curtain and one floating card"
    ),
    "BELIEVER-COVENANT": (
        "two parties linked by a golden covenant ribbon of light, covenant nodes lighting up with "
        "divinity light and shield sigils at the trigger"
    ),
    "BELIEVER-MANIFESTATION": (
        "a huge translucent holy apparition rising behind the Believer, extra glowing pages unfolding "
        "in its palms"
    ),
    "BELIEVER-CHANNEL": (
        "a circular energy dial around the Believer, three light nodes lighting in order, a shield and "
        "a drawing page at its center"
    ),
    # ---- 普通神迹（12）----
    "BELIEVER-LET_THERE_BE_LIGHT": (
        "a colossal white light splits the darkness open from the top of the frame, enemy silhouettes "
        "recoiling inside the curtain of light, the Believer's hooded silhouette kept at the center"
    ),
    "BELIEVER-GENESIS": (
        "a cosmic clock-like wheel of re-creation unfolding behind the Believer, time runes turning "
        "backward, the outline of a previous form kept in the foreground"
    ),
    "BELIEVER-FOUR_HORSEMEN": (
        "silhouettes of the four horsemen of the apocalypse surround the frame from four directions, "
        "the Believer at the center as their summoner, four layers of sacred light in distinct colors"
    ),
    "BELIEVER-PARTING_OF_THE_RED_SEA": (
        "tsunami-like walls of water part from the center around a road of white-gold light, the "
        "Believer standing mid-sea, robes blown by the gale"
    ),
    "BELIEVER-TEN_PLAGUES": (
        "ten orbs and scars of plague falling one after another from the sky onto different enemy "
        "positions, not a single huge explosion"
    ),
    "BELIEVER-FALL_OF_JERICHO": (
        "a gigantic city wall collapsing at the sound of a golden horn, wall stones and armor "
        "fragments bursting outward, the Believer raising a hand in the foreground"
    ),
    "BELIEVER-JUDGMENT_OF_FIRE": (
        "a single pillar of heavenly fire piercing the main target, flames spreading to surrounding "
        "enemies after it shatters, a vertical column rather than a full-screen sea of fire"
    ),
    "BELIEVER-SEVEN_SEALS": (
        "seven golden seals unfolding and cracking one after another, each broken seal releasing a "
        "sevenfold strike of light and one greater final blow descending at the end"
    ),
    "BELIEVER-FIVE_LOAVES_AND_TWO_FISH": (
        "holy light symbols of five loaves and two fish circling an unfolded card, three glowing page "
        "duplicates flying out of the symbols"
    ),
    "BELIEVER-LAZARUS_RISES": (
        "the shadow of a tomb and sarcophagus cracking open, three cards pulled back by white "
        "resurrection ribbons of light, the Believer extending one hand"
    ),
    "BELIEVER-LORD_OF_HOSTS": (
        "the firmament splitting open as countless silhouettes of angelic wings and spears dive down, "
        "each spear impact corresponding to one trigger"
    ),
    "BELIEVER-WHITE_THRONE_JUDGMENT": (
        "a white throne and a judgment halo of light high at the top of the frame casting down a giant "
        "pillar of light, the boss outline and low-health enemies marked by different light trails"
    ),
}


# ---------------------------------------------------------------------------
# 3. 提示词构建
# ---------------------------------------------------------------------------


def _scene(row: Mapping[str, str]) -> str:
    """取该行的英文场景描述；没有翻译时回退到规格表原文。"""
    card_id = (row.get("card_id") or "").strip()
    scene = VISUAL_EN.get(card_id)
    if scene:
        return scene
    raw = (row.get("recommended_visual") or "").strip()
    if not raw:
        raise ValueError(f"卡牌 {card_id or '<unknown>'} 缺少 recommended_visual，无法构建提示词")
    return raw


def build_prompt(row: Mapping[str, str], variant: str = "normal") -> str:
    """根据规格行生成 Agnes 英文提示词。

    :param row: ``M10.2.1-CardArt-Spec.csv`` 的一行（dict）。
                强化任务（``variant="enhanced"``）请传入**对应的普通神迹行**，
                保证主体与构图完全一致，只增强光效。
    :param variant: ``normal`` / ``miracle`` / ``enhanced``。
    """
    if variant not in COMPOSITION:
        raise ValueError(f"未知变体 {variant!r}，可选: {', '.join(VARIANTS)}")

    parts: list[str] = [STYLE_PREFIX, CHARACTER, f"Scene: {_scene(row)}"]
    if variant == "miracle":
        parts.append(MIRACLE_EMPHASIS)
    elif variant == "enhanced":
        parts.append(ENHANCED_RADIANCE)
    parts.append(f"Composition: {COMPOSITION[variant]}")
    parts.append(STYLE_SUFFIX)

    cleaned: list[str] = []
    for part in parts:
        text = part.strip()
        if not text:
            continue
        if not text.endswith((".", "!", "?")):
            text += "."
        cleaned.append(text)
    return " ".join(cleaned)


def build_sheet_title(count: int, mode: str) -> str:
    """接触表（contact sheet）标题用的英文文案。"""
    return f"Believer card art - {count} images ({mode})"


__all__ = [
    "PROMPT_VERSION",
    "VARIANTS",
    "Variant",
    "STYLE_PREFIX",
    "CHARACTER",
    "FULL_BLEED",
    "NEGATIVE",
    "STYLE_SUFFIX",
    "ENHANCED_RADIANCE",
    "MIRACLE_EMPHASIS",
    "COMPOSITION",
    "VISUAL_EN",
    "build_prompt",
    "build_sheet_title",
]

# Believer 正式美术参考图生成提示词

> 参考源：`Assets/Reference/神秘金饰长袍祭司姿态设定集.png`（1448×1086，6 个姿态）。
> 另一个概念参考：`Assets/Reference/Believer_Concept.png`。
> 当前 DSH 环境没有图像生成能力，因此本文件是**可直接交给图像生成工具的完整提示词**；未生成正式 `Believer_Model_Reference.png` 等美术文件。

## 推荐生成流程

1. 将 `神秘金饰长袍祭司姿态设定集.png` 作为 **image reference / style reference / character reference** 输入图像生成工具。
2. 使用下面的完整提示词生成 **一张** 3:2 或 16:10 的模型参考板；如果一次生成无法保证三个视图一致，则按“三视图 / 头部细节 / 服装分层 / 战斗小人”分四次生成，固定同一 character reference 和 seed，再在图像编辑器中拼成一张板。
3. 生成后只作为**建模与贴图参考**；不要把整张设定图或拼板直接缩成游戏图集。
4. 需要保留可编辑源文件（PSD/CLIP/Krita 分层文件），后续由美术按 Silent 附件结构拆分绘制。

## 完整正向提示词（English，可直接复制）

```text
Create a single production-ready character model reference sheet for a hand-painted fantasy game character named "Believer", designed for Slay the Spire 2's small in-combat character scale. Use the supplied pose sheet as the character-design reference: ivory-white layered robes, antique-gold edging, a tall pointed hood, a completely shadowed face, and an austere, mysterious high-priest presence. This must be an original hooded believer, not a recolored Slay the Spire Silent.

CANVAS AND LAYOUT
- Large landscape sheet, 3:2 aspect ratio, high resolution, dark desaturated blue-grey studio background with a subtle warm spotlight.
- No written labels or logos inside the image; leave clean margins where labels can be added later.
- Panel A (left 60% width, top 70% height): full-body character turnaround, three views standing on the same ground line, exactly the same height, proportions and costume: (1) front view, (2) side profile facing right, (3) back view.
- Panel B (top right): three close-up head-and-hood views: front, right profile, and back; plus one small close-up of how the hood meets the layered shoulder mantle.
- Panel C (middle right): clean exploded clothing-layers diagram on the same dark background: outer hood, inner robe, layered shoulder mantle, left wide sleeve, right wide sleeve, front tabard/skirt panel, layered robe hem, circular chest seal, and narrow antique-gold trim bands. Each item isolated, clearly separated, same palette and design language.
- Panel D (bottom strip): a small side/three-quarter combat-scale reference figure facing right, full body, feet planted, one hand raised in a calm gesture of channeling divine power, other hand relaxed; no weapon, no magic effects, no halo, no orbs; robe and cape must not hide the body silhouette.

CHARACTER DESIGN
- Head and hood: tall, dignified pointed hood with a layered cloth crown; outer hood ivory-white with warm grey folds and restrained antique-gold edging; deep charcoal-black interior with NO visible face, eyes, nose, mouth, skull, hair, antlers or any recognizable Silent features. The face opening is a soft dark void, not a flat black oval; the hood has real fabric thickness and a shadowed inner rim. Hood outer silhouette is clearly larger and more imposing than the shoulders.
- Shoulders: two or three layered shoulder mantles/capes with broad but compact silhouette; crisp ivory cloth, warm grey shading, thin antique-gold borders; layered edges clearly readable at small scale; no oversized shoulder spikes.
- Torso: fitted ivory-white inner robe with warm grey-brown folds; a large, clear circular golden chest seal as the focal icon: simple outer ring, inner ring, central disc, and four short geometric rays, slightly raised metallic gold; no dense filigree.
- Waist: narrow antique-gold belt with a simple central clasp; restrained geometric trim.
- Sleeves: broad, clean bell sleeves with ivory cloth folds and restrained gold cuff bands; hands pale grey/desaturated, natural and empty, no weapons, no extra fingers.
- Lower body: layered long robe with a strong vertical silhouette; front tabard with a simple gold vertical motif; angular but clean hem points; moderate width, not excessively long or wide; enough leg/robe separation to read as a standing figure at game scale.
- Back: hood point and layered mantle visible; back cape split into two clean tail panels with gold edging; robe back has simple broad folds; no chest seal on the back.
- Gold decoration: antique gold, slightly desaturated, hand-painted highlights; use thicker, clearer strokes suitable for a small game character; limit ornaments to hood edge, mantle edges, cuffs, belt, tabard hem, and chest seal.
- Palette: warm ivory #E9E1D0, pale bone #F5EFE3, warm grey-beige shadow #B7AB96, deep umber shadow #5E5243, charcoal hood void #1B1917, antique gold #B08A4F, gold highlight #D8BB76, dark gold #6F5529.
- Lighting: soft top-left key light, warm bounce from below, strong readable dark-to-light cloth folding; painterly hand-painted fantasy game art, crisp silhouette, restrained texture, no plastic specular, no photorealistic skin or fabric.
- Proportions for a Slay the Spire 2 combat character: compact and dignified; head plus hood approximately 28% of total height; shoulders approximately 22% of total height wide; body slightly tapered; total silhouette readable at roughly 240–300 pixels tall. In the combat-scale panel, simplify the seal and trim so they remain readable when reduced. Avoid tiny chains, micro-engravings, thin dangling ribbons, or accessories that disappear at game scale.

CONSISTENCY REQUIREMENTS
- All three turnaround views and all detail panels show the same character, same costume construction, same hood shape, same shoulder layers, same sleeve shape, same hem length, same gold ornament placement, same chest-seal design.
- Correct anatomy, natural hands, exactly two arms and two legs, no duplicated limbs, no inconsistent garment layers, no weapons.
- The face remains completely hidden in every view and pose.
- Keep the design clearly distinct from Silent: no skull face, no antlers, no green/teal Silent cloth, no dagger holsters, no bandage-wrapped limbs as the main silhouette.

STYLE
Hand-painted fantasy game illustration in the visual spirit of Slay the Spire 2: painterly cloth, simplified shapes, strong value grouping, warm ivory and antique gold, dark mysterious hood, solemn and slightly oppressive sacred presence. Concept-art quality, not 3D render, not anime, not photorealistic, not plastic.

The final sheet should read as a professional model sheet that a 2D Spine artist can use to redraw the head/hood, torso, shoulder cloak, sleeves, dress/skirt and cape as separate attachments.
```

## 负向提示词（English）

```text
visible face, eyes, nose, mouth, skull face, skull, antlers, horns, hair, Silent character, green cloth, teal fabric, daggers, weapons, extra arms, extra hands, extra fingers, deformed hands, asymmetrical costume, inconsistent hood shape, tiny filigree, micro details, excessive ornaments, giant shoulder spikes, trailing ribbons, halo, glowing orbs, magic VFX, wings, photorealistic 3D render, plastic material, glossy skin, anime face, cute chibi, sexy priest, heavy armor, text, labels, watermark, logo, cropped feet, multiple characters in one panel, inconsistent scale, busy background, noisy texture
```

## 工具补充要求

- 如果生成工具支持 **character reference / image prompt / style reference**，优先使用 `神秘金饰长袍祭司姿态设定集.png`；如果支持多参考图，再加入 `Believer_Concept.png` 作为胸口圣徽与背视图补充。
- 如果生成结果三视图不一致：不要接受；固定同一种子、同一角色参考，分图生成后拼板。
- 如果生成工具输出文字乱码：接受无文字版本；由美术后期在排版工具中添加中文/英文标签。
- 生成分辨率建议至少 2400×1600；头部细节局部图至少 1024×1024。
- 保留生成参数、种子和参考图路径；最终分层源文件保存为 PSD/CLIP/Krita，不只保留拼板 PNG。

## 预期输出文件名（需要图像生成工具执行）

- `Assets/Reference/Believer_Model_Reference.png` — 正式模型参考板。
- `Assets/Reference/Believer_Turnaround.png` — 三视图单独输出（可选）。
- `Assets/Reference/Believer_Hood_Detail.png` — 头部/兜帽细节（可选）。
- `Assets/Reference/Believer_Clothing_Parts.png` — 服装分层图（可选）。
- `Assets/Reference/Believer_Combat_Side.png` — 战斗小人侧身参考（可选）。

> 当前状态：以上 PNG **尚未生成**，因为 DSH 环境没有图像生成能力。已有 `Believer_Model_Reference_WorkingBoard.png` 是从提供的姿态设定集裁切、标注的工作参考板，不是新生成的正式美术。

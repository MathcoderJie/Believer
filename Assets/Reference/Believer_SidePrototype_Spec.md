# M9-A2.7 侧身角色原型与技术适配规范

> 本文件只做美术原型规范与技术可行性评估，不修改现有源码、骨骼、动画和正式角色资源。
> 当前工作树保留 A2.3/A2.1 未提交修改；未执行 reset/clean/stash。

## 1. 输入与检查结果

- 分支：`main`
- 当前 HEAD：`b740bee`（A2.1 checkpoint）
- 参考图实际存在：
  - `Assets/Reference/暗影圣职者角色设定稿.png`（1448×1086）：**正式模型参考板**，包含三视图、头部/兜帽细节、服装分层拆解、战斗小人、尺寸参考、主色板和设计要点。
  - `Assets/Reference/神秘金饰长袍祭司姿态设定集.png`（1448×1086）：姿态库，用于动作/布料参考。
  - `Assets/Reference/Believer_Concept.png`：辅助概念参考。
- `Assets/Reference/Believer_Model_Reference.png` **仍不存在**；当前以 `暗影圣职者角色设定稿.png` 作为等价模型参考。
- 参考图来源裁切目标：[`Believer_SidePrototype_Target.png`](Believer_SidePrototype_Target.png)（明确标注为 source crop / target only，不是新绘制的正式美术，也不可直接作为游戏图集）。

## 2. Spine 转换管线验证结果

上一轮 A2.3 已实际验证：

| 项目 | 结果 |
|---|---|
| 二进制骨架解析 | Spine **4.2.43**，**141 bones / 56 slots / 1 skin（57 attachments）** |
| 约束 | **2 IK + 11 Transform**，0 Path/Physics |
| 动画 | **7 个**：attack, cast, die, hurt, idle_loop, relaxed_loop, shiv |
| 二进制 → JSON | 已生成完整 JSON；骨骼/附件/约束/动画时间线/网格权重可解析 |
| DeformTimeline | 按 slot/attachment 对比，顶点偏移 **0 差异** |
| 游戏内加载 | 基础 JSON + 自定义 atlas 成功加载全部 7 个动画 |
| 自定义附件 | 新增 hood mesh / slot / slot 顺序可加载并播放 idle/attack/cast/hurt/die |
| 已知修正 | 转换器需处理 drawOrder 有符号 offset、alpha 的 `value` 键、inherit 的 `inherit` 键、TransformConstraint 全混合值；已在验证流程中修正 |

**结论：骨架转换/自定义 skeleton JSON 加载管线可用，可以作为后续附件原型的技术基础。**

**但**：这不等于正式美术已完成。上一轮的 hood mesh 是程序化技术原型，外观不能作为信徒正式美术验收。

## 3. 侧身原型目标（游戏内 240–300 px 尺度）

- 总高度：**约 244–280 px**（以 Silent 场景 Bounds/scale 为基准，实际再按新场景校准）。
- 头部+兜帽：**60–78 px**，占总高 **25–30%**。
- 肩部披肩总宽：**50–65 px**，紧凑多层，不能宽到与披风混成一团。
- 躯干宽度：约 **42–55 px**；避免 Silent 前倾、窄腰、猎手肩线。
- 长袍下摆宽：**≤ 80 px**，纵向轮廓清晰，覆盖腿部。
- 胸口圣徽：**20–28 px**，外环+中央圆盘+四向短光芒，缩小后仍清楚。
- 金色镶边：**2–3 px**（游戏尺寸），避免 1px 细线和微雕。
- 侧身方向：面向画面右侧，轻微 3/4；双脚自然站立，一手下垂、一手略微抬起；不持武器。
- 披风不能覆盖整个身体；从侧面必须能读出“尖顶兜帽—层叠肩披—纵向白袍—金圣徽”。

## 4. 分层绘制规范

> 下列图层是**独立可绑定部件**，不是把多姿势参考图直接切进图集。正式绘制时使用透明背景分层源文件（PSD/CLIP/Krita），再按 UV 映射到信徒自己的 atlas。

| 顺序（后→前） | 图层名 | 绑定骨骼 | 绑定 slot / 处理 | 目标源尺寸（2×） | 备注 |
|---:|---|---|---|---|---|
| 1 | `cape_back_main` | `capeBase`/`cape_corner_l/r`/`cape_center` | 可复用/新建 `cape` slot | 420×420 | 后背主披风，攻击时缩小/收窄 |
| 2 | `cape_side_tail_l` | `hip_cover`/`leg_upper_r` 区域 | 新附件 | 220×360 | 左侧长尾，随腿摆动 |
| 3 | `cape_side_tail_r` | `leg_upper_r`/`leg_flap` | 新附件 | 220×360 | 右侧长尾，避免遮挡胸口 |
| 4 | `skirt_back` | `crotch_cloth2..6` | 新附件 | 320×380 | 后裙层，垂直褶 |
| 5 | `skirt_front_tabard` | `crotch_cloth`/`crotch_cloth99` | 新附件 | 260×380 | 前襟纵向 + 简化金纹 |
| 6 | `torso_robe` | `body_1/body_2/body_3` | 新附件或替代 `torso copy` | 300×300 | 收紧躯干，避免猎手前倾 |
| 7 | `chest_seal` | `body_1`/`body_2` | 新附件（独立 slot 或纹理叠加） | 128×128 | 单独高对比金色，避免被 deform 拉花 |
| 8 | `sleeve_l` | `arm_upper_l`/`arm_lower_l`/`arm_flap_l*` | 新附件 | 220×300 | 宽袖+金袖口 |
| 9 | `sleeve_r` | `arm_upper_r`/`arm_lower_r`/`arm_flap_r*` | 新附件 | 220×300 | 右手微抬，袖口不挡圣徽 |
| 10 | `shoulder_mantle_back` | `body_3`/`shoulder_l/r`/`capeBase` | 新附件（后层） | 360×220 | 多层披肩第 1 层 |
| 11 | `shoulder_mantle_front` | `body_3`/`shoulder_l/r` | 新附件（前层） | 360×220 | 金边、分片尖角 |
| 12 | `hood_back_shell` | `head`/`head_twist`/`body_3` | 新 slot `believer_hood_back`（head 之前） | 360×400 | 尖顶轮廓、深色内衬 |
| 13 | `hood_front_rim` | `head`/`head_twist` | 新 slot `believer_hood_rim`（head 之后） | 360×400 | 象牙白外层+古金边 |
| 14 | `face_shadow` | `head` | `head` slot 纹理/新附件 | 180×220 | 纯深黑空洞，无五官、无骷髅/头发 |
| 15 | `hood_lining` | `head`/`head_twist` | 新附件（在 head 之前） | 260×300 | 深灰黑内衬，形成厚度 |
| 16 | `gold_trim_bands` | 各部件自身 | 画在各部件层内 | 256×64 | 只保留主边，避免微雕 |

**绘制顺序建议：**先画身体/内袍的暗部，再叠肩披、兜帽后壳、面部阴影、兜帽前缘、袖子、前襟和圣徽；金色边线最后统一收边，保持 2–3 px 的游戏可读粗细。

### 兜帽结构（重点）

- 尖顶要明确：顶部略向前倾，顶点在角色正面/侧面都能读成“尖顶兜帽”，不是圆帽。
- 外层象牙白布料分 2–3 个面：高光面、灰褐暗面、内翻边；边缘用古金线收边。
- 内部为真正的深黑空洞：不要画眼睛、鼻子、嘴、骷髅、头发、鹿角或 Silent 面部残留。
- 兜帽与肩披连接处要有重叠布片，不能悬空；后壳要盖住颈部并接上肩披。
- 侧面看，兜帽后缘要有厚度和垂坠，面部开口偏前，不能像一个圆形光环。

### 肩披/袖/下摆

- 肩披至少 2 层：后层宽、前层窄，层间有阴影和金色边；不要做成巨大翅膀。
- 袖子宽而不拖地；袖口金边集中在末端 1–2 道，避免细碎纹样。
- 下摆分前襟、左右侧片、后裙层；前端尖角清晰，侧面能看出布料层叠；不遮住全身、不拖出细长飘带。
- 腿部尽量用深色内袍阴影遮住，不保留 Silent 的白色绷带/脚部碎块。

## 6. 技术实现方案

### 6.1 附件与 slot

- **不覆盖原版 Silent**：全部新资源放在信徒自己的路径/atlas 中，原 Silent skeleton/atlas 不写入。
- **保留旧定义**：`head`、`skull`、`hair`、`hood top`、`bottom hood`、`torso copy`、`cloak back`、`cape_front_attack` 等旧附件定义保留；在信徒 skin/自定义 skeleton 中把他们替换为隐藏（slot attachment 设为 null）或保留但不显示。
- **新附件**：优先新 MeshAttachment，权重到已有动画骨骼（head/body/arm/cape/crotch_cloth），这样能直接继承 7 个原生动画，不需要新动画。
- **新 slot 顺序**：
  1. `believer_hood_back`（head slot 之前，后壳/内衬，画在头部后面）
  2. `believer_cape_back`（身体后层）
  3. `head`（深色面部阴影）
  4. `believer_hood_rim`（head 之后，兜帽前缘，画在面部外圈）
  5. `believer_shoulder_*`（身体前层，按需）
  6. `believer_sleeve_*`、`believer_skirt_*` 等按实际前后遮挡排序。
- **不要直接用旧 deform 网格改拓扑**：`hood top`、`torso copy`、`cloak back`、`right cape`、`cape_front_attack` 等有 DeformTimeline；如果必须复用，保持顶点数量/顺序/权重，否则新建附件并让旧附件隐藏。
- **攻击披风**：`cape_front_attack`/`cape_back_attack` 的原网格过大；正式版应重新绘制/缩小，并在 attack/shiv 动画中只保留小幅动势，避免半透明大布片遮挡。
- **死亡动画**：`die` 会切换 `death_hood`/`death_skull`/`death_torso` 等；新兜帽/袍必须有死亡版，或在 die 动画中通过 slot attachment timeline 隐藏普通版、显示死亡版。不能让原型 back 层在死亡动画中残留。

### 6.2 动画复用

| 动画 | 目标表现 |
|---|---|
| `idle_loop` | 尖顶兜帽轻微随头部；肩披/下摆有细小布料摆动；圣徽清楚 |
| `attack` | 无武器；宽袖/披肩有动势；攻击专用披风缩小，不遮身体 |
| `cast` | 抬头或抬手时面部仍是黑暗空洞；袖口/披风不穿模 |
| `hurt` | 头部/兜帽稳定，不脱离颈部；披肩不穿入躯干 |
| `die` | 使用死亡版兜帽/袍；普通版 hood back/rim 隐藏；不露出 Silent 头部 |
| `relaxed_loop`/`shiv` | 后续可选；原型阶段至少验证 idle/attack/cast/hurt/die |

### 6.3 验证矩阵

1. **加载校验**：自定义 skeleton JSON + 信徒 atlas；7 个动画名/时长正确。
2. **静态校验**：侧身尺寸、头部/兜帽比例、肩宽、圣徽像素直径、金边宽度。
3. **动画校验**：idle/attack/cast/hurt/die 各截帧；检查面部、颈部连接、披肩/袖/裙穿模、UV 缺口。
4. **对比**：同尺寸下与 Silent 原版并排；信徒必须明显是兜帽祭司，排除“白色 Silent”。

## 7. 正式素材清单（尚未提供）

需要美术提供以下透明分层源文件（建议 2× 分辨率，PSD/CLIP/Krita 可编辑）：

- `Believer_Side_Prototype.psd` — 完整侧身分层原型（本阶段核心交付）。
- `Believer_Hood_Outer.png` — 尖顶兜帽外层（象牙白+金边）。
- `Believer_Hood_Lining.png` — 兜帽内衬/深黑空洞。
- `Believer_Hood_Rim.png` — 兜帽前缘金边/厚度。
- `Believer_Face_Shadow.png` — 无五官面部阴影。
- `Believer_Shoulder_Back.png`、`Believer_Shoulder_Front.png` — 多层肩披。
- `Believer_Torso_Robe.png` — 象牙白内袍。
- `Believer_Chest_Seal.png` — 圆圣徽。
- `Believer_Sleeve_L.png`、`Believer_Sleeve_R.png` — 宽袖+金口。
- `Believer_Skirt_Front.png`、`Believer_Skirt_Back.png` — 长袍下摆。
- `Believer_Cape_Back.png`、`Believer_Cape_Tails.png` — 披风/尾摆。
- `Believer_Attack_Cape_Front.png`、`Believer_Attack_Cape_Back.png` — 缩小后的攻击披风。
- `Believer_Death_Hood.png`、`Believer_Death_Robe.png` — 死亡动画版本。

**建议源尺寸：** 单件 256–512 px 长边；最终 atlas 可 618×523（Silent 原布局）或 2× 1236×1046 自建页面；不要超过约 2048 以保证移动端友好。

## 8. 当前状态与未完成项

**已完成：**
- 参考图检查与缺口报告。
- Silent 骨架/JSON 转换与加载管线验证（7 个动画、57 附件、约束、deform 校验）。
- 侧身原型比例、分层结构、slot/骨骼绑定方案。
- [侧身目标参考板](Believer_SidePrototype_Target.png)（source crop，仅目标/度量参考，不是新美术）。
- 分层 manifest：[`Believer_SidePrototype_LayerManifest.json`](Believer_SidePrototype_LayerManifest.json)。

**尚未完成（当前环境无图像生成/专业绘画工具，不能伪造）：**
- 正式手绘的侧身小人分层 PNG/PSD。
- 这些图层的最终 UV 拆分/atlas 打包。
- 接入信徒测试场景后的实际游戏内截图。
- idle_loop / attack / cast / hurt / die 的正式美术验证（目前只有技术管线验证，不是正式美术验收）。
- `Assets/Reference/Believer_Model_Reference.png` 仍未生成；现有 `暗影圣职者角色设定稿.png` 可作为等价模型参考。

## 9. 下一步

1. 由美术/图像生成工具按 `Believer_Model_Reference_Prompt.md` 或 `暗影圣职者角色设定稿.png` 输出正式侧身分层图。
2. 将图层按本规范绑定到 `head`/`body_*`/`arm_*`/`capeBase`/`crotch_cloth`，先做一个不覆盖 Silent 的自定义 skeleton JSON。
3. 用现有 dev harness 加载信徒自己的 `.spjson + atlas`，实际验证 idle/attack/cast/hurt/die。
4. 只有正式分层美术通过验收后，才进入身体比例/骨架级重塑；本阶段不做全知全能特效。

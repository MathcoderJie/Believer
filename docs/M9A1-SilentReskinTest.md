# M9-A1：Silent 原生骨架换皮最小可行测试报告

> 基线 Git：`a7109df`（开始前工作区干净）。
> 本阶段只做技术验证与最小换皮：**复用 Silent 原生 Spine 骨架/动画**，使用独立图集；**不替换正式信徒角色、不覆盖原版 Silent 资源、不改卡牌/神性/神迹/遗物逻辑**。

## 1. 验证结论（先给结论）

| 验证项 | 结果 |
|---|---|
| 原生 Silent `SpineSkeletonDataResource` 读取 | **通过**：`ResourceLoader.Load("res://animations/characters/silent/silent_skel_data.tres")` → `SpineSkeletonDataResource`；取出 `skeleton_file_res`（`SpineSkeletonFileResource`）与 `atlas_res` 成功 |
| 原生骨架文件 + 自定义 Atlas 组合 | **通过**：新建 `SpineSkeletonDataResource`，`skeleton_file_res` 用原生 Silent，`atlas_res` 用自研图集，`load_from_atlas_file` 返回 0 |
| 组合后可播放动画 | **通过**：7 个动画可读并实际播放 `idle_loop / attack / cast`（另有 `die / hurt / relaxed_loop / shiv` 原样保留） |
| 自定义资源被 Mod 加载/打包 | **通过**：图集与页面 PNG 作为 EmbeddedResource 打进 DLL，运行时写入 `user://` 并成功加载；测试场景打进 PCK |
| 原版 Silent/现有信徒功能 | **不受影响**：未写入任何原版 `res://` 路径；未改 Believer 正式视觉；正常启动（无测试标记）不出现测试覆盖层，M8 功能日志正常 |

**结论：换皮技术路线可行**。原生骨架、蒙皮、动画曲线全部保持不变，只替换为独立图集即可显示与播放；后续可在此骨架上做完整区域重绘。

## 2. 新增与修改的文件

| 文件 | 说明 |
|---|---|
| `BelieverCode/ReskinTest/silent_test.atlas` | **新增**：基于 Silent 原版 atlas 布局生成的测试图集文本；保留全部区域名称、bounds、offsets、rotate、page size/filter/scale；仅把页面文件名改为 `silent_test.png`（不覆盖原版）。 |
| `BelieverCode/ReskinTest/silent_test.png` | **新增**：由原版 Silent 页面（618×523，从 `.ctex` 解码）安全重着色的测试页面；只调整 19 个布料/徽章区域为象牙白/金色，其余保持原样（武器、特效、死亡相关区域均未清空）。 |
| `BelieverCode/ReskinTest/.gdignore` | **新增**：防止 Godot 再导入/打包这些源图（它们通过 EmbeddedResource 进 DLL）。 |
| `BelieverCode/Dev/SilentReskinTest.cs` (+`.uid`) | **新增**：dev-only 测试入口；仅在标记文件 `user://believer_reskin_test.flag` 存在时运行，生成三份测试角色分别播放 `idle_loop/attack/cast` 并输出日志；不替换正式角色。 |
| `Believer/reskin_test/silent_test_visuals.tscn` | **新增**：独立测试场景（根 Node2D + SpineSprite，scale 0.29），不修改 Silent 原生场景。 |
| `Believer.csproj` | **修改**：仅新增两条 `EmbeddedResource`（测试 atlas/png），用于运行时加载；不改变现有构建/依赖。 |
| `docs/images/M9A1-silent-reskin-test.png`、`docs/.gdignore` | **新增**：游戏内截图与 docs 排除导入规则。 |

没有修改：`BelieverCode/Character/Believer.cs`、卡牌、神性/全知全能/神迹/遗物逻辑。

## 3. 测试资源生成方式（可复现）

1. 从游戏 PCK 提取 Silent 的 `silent.png-*.ctex`、`silent.atlas-*.spatlas`、`silent.skel-*.spskel`。
2. 用 MegaDot headless 把 `.ctex` 解码为原版 618×523 页面 PNG。
3. 解析 `.spatlas` 里的完整 atlas 文本（56 个区域），**原样保留**所有区域名/bounds/offsets/rotate 与页面属性。
4. 只对以下 19 个布料/徽章区域做安全重着色（象牙白/金色）：  
   `back arm cloak`、`back arm cloak back`、`badge`、`bottom hood`、`cape_back_attack`、`cape_front_attack`、`cape_front_blocking`、`cloak back`、`death_half_scale/death_hood`、`death_half_scale/death_skirt back`、`death_half_scale/death_skirt fabric`、`death_half_scale/death_spine sash`、`death_half_scale/death_torso`、`dress`、`hood top`、`left cape`、`right cape`、`top arm cloak`、`torso copy`。
5. 其余区域（面部/角/身体/手/武器/特效等）完全保留原版；未清空任何武器、特效或死亡区域。

## 4. 游戏内验证结果

测试标记：`%APPDATA%\SlayTheSpire2\believer_reskin_test.flag`（测试后已删除，正常启动不再显示测试角色）。

**运行日志（真实游戏进程）**：
```
[SilentReskinTest] native silent data: SpineSkeletonDataResource
[SilentReskinTest] native skeleton_file_res=SpineSkeletonFileResource, atlas_res=SpineAtlasResource
[SilentReskinTest] custom atlas load err=0
[SilentReskinTest] combined data: 7 animations [attack, cast, die, hurt, idle_loop, relaxed_loop, shiv]
[SilentReskinTest] idle_loop: current=idle_loop
[SilentReskinTest] attack: current=attack
[SilentReskinTest] cast: current=cast
[SilentReskinTest] overlay added (remove user://believer_reskin_test.flag to disable).
```
- 无 `missing region`、UV 警告或异常日志。
- 三份角色分别播放 `idle_loop / attack / cast`，姿势明显不同，兜帽/披风/裙摆附件正确绑定并随动画运动；未观察到透明缺口、UV 错位或异常闪烁。
- **游戏内截图**：[docs/images/M9A1-silent-reskin-test.png](docs/images/M9A1-silent-reskin-test.png)（主菜单上叠加三份测试角色；Silent 原生骷髅面部/角/武器仍在，因为本阶段只改少量布料区域）。

## 5. Build / Publish / 部署

- `dotnet build Believer.csproj`：**0 warning / 0 error**
- `dotnet publish -p:PublishProfile=FolderProfile`：**exit 0**
- 部署（`D:\1\steamapps\common\Slay the Spire 2\mods\Believer\`）：
  - `Believer.dll` 389,632 B（含测试图集/PNG）
  - `Believer.pdb` 56,448 B
  - `Believer.pck` 234,680 B（包含 `res://Believer/reskin_test/silent_test_visuals.tscn`；不包含源测试资源路径）
- 正常启动（无测试标记）冒烟通过：M8 功能日志正常，未出现 `[SilentReskinTest]`。

## 6. 复现步骤

1. 构建并发布：`dotnet build`、`dotnet publish -p:PublishProfile=FolderProfile`。
2. 创建标记：`%APPDATA%\SlayTheSpire2\believer_reskin_test.flag`（内容任意）。
3. 启动游戏：主菜单会出现三份测试角色（idle/attack/cast），日志输出上述验证信息。
4. 截图后删除标记文件即可关闭测试入口。

## 7. 目前换皮方案的限制

1. **只换了颜色，不是最终信徒外观**：Silent 的骷髅面部、角、身体绷带、匕首/特效仍保留；当前截图只能证明“原生骨架 + 独立图集 + 动画”链路可行。
2. **附件几何受原骨架限制**：Texture 重绘不改变附件边界/枢轴/网格；Silent 的兜帽是“角+兜帽”结构，要变成纯色深兜帽，需要重绘对应区域（角可涂透明/隐藏，但需确认其所有动画引用）。
3. **区域覆盖未完成**：本阶段只对 19 个布料/徽章区域重着色；完整 Believer 需要重绘全部身体/服装区域（尤其 torso、手臂、手、腿、dress/skirt 的所有变体），并把武器/特效区域透明化或改成金色仪式效果。
4. **未测试图集重排/放大**：当前严格沿用原 atlas 布局、原页面尺寸（618×523）与 scale 0.32；若后续想提高细节分辨率或重排区域，需要额外测试 Spine atlas 的 scale/bounds 兼容性。
5. **死亡/攻击专用附件**：Silent 有 `death_half_scale/*`、`cape_*_attack` 等变体；本阶段未清空且保留，后续重绘时必须全部覆盖，否则会出现不一致外观。
6. **全知全能相关**：本阶段只验证基础换皮；悬浮/七重神环/光球/地面圣印/1.5 秒变身尚未实现。悬浮可先用节点位移 + 现有披风骨骼；神环/光球/圣印将作为独立节点，不写入角色骨架。

## 8. 下一步（等待确认后）

1. 恢复参考图到 `Assets/Reference/Believer_Concept.png`（仅该图片，不恢复整个 stash）。
2. 制定 Silent 区域 → 信徒部件的完整重绘映射（兜帽、披风、白袍、袖/手、腿、胸印）。
3. 完成第一版全区域图集（保留所有区域名/尺寸/旋转/偏移），并在同一测试入口截图确认无缺失区域。
4. 通过后接入正式信徒 `CreateCustomVisuals()`，再实现悬浮/神环/圣印/1.5 秒演出。

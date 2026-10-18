# M9-A2：信徒正式外观重绘（Silent 原生骨架复用）

> 基线：M9-A1 已验证“原生 Silent 骨架 + 独立图集”可加载，`idle_loop / attack / cast` 可播放。
> 本阶段在该可靠链路上完成第一版完整信徒换皮，并保留全部原生骨骼/蒙皮/动画时间轴。

## 1. 前置检查

- Git 开始状态：A1 改动未提交、工作区非干净但与 M8 基线一致；未重置/删除任何源码。
- M9-A1 结论：原生 `SpineSkeletonDataResource` 可读，自定义 atlas 可组合，三种动画实际播放通过。
- 参考图：仅从 stash 恢复 `Assets/Reference/Believer_Concept.png`（单文件，未恢复整个 stash）。
- 原有卡牌/神性/神迹/遗物/全知全能逻辑未改动。

## 2. 新图集与原生图集的对应关系

- 原生 Silent atlas：1 页 `silent.png` 618×523，`scale:0.32`，**56 个区域**（bounds / offsets / rotate 完整保留）。
- 新图集：`BelieverCode/ReskinTest/believer_skin.png` + `believer_skin.atlas`，**同一页尺寸、同一 scale、同样 56 个区域名与布局**；atlas 文本除页面文件名外完全复制。
- 骨骼、蒙皮、网格 UV、附件引用、动画时间轴：**完全使用原生 Silent 数据，未改动**。
- 测试入口只替换 atlas texture；原版 Silent 资源未被覆盖。

## 3. 实际重绘的角色区域

| 类别 | 区域（实际名称） | 处理 |
|---|---|---|
| 兜帽外层 | `hood top`、`left cape`、`right cape` 中兜帽部分 | 象牙白布料 + 金色 2px 边缘 + 褶皱明暗 |
| 兜帽阴影/内衬 | `bottom hood` | 深灰近黑内衬、金色边 |
| 面部 | `head` | 不再使用原头骨/脸所在皮肤，改为椭圆的深色兜帽开口 + 细金边；`skull`、`hair`、`hair top`、`eye`、`bottom horn`、`top horn`、`death_antlers`、`death_hair`、`death_skull` 像素透明（区域与引用保留，不删除） |
| 披风/长袍 | `cloak back`、`cape_front_attack`、`cape_front_blocking`、`cape_back_attack`、`death_hood`、`death_skirt back`、`death_skirt fabric`、`dress`、`back arm cloak`、`back arm cloak back`、`top arm cloak` | 象牙白/米白多层布料 + 灰白背光暗部 + 金色边缘 |
| 躯干与胸前圣印 | `torso copy`、`death_torso` | 象牙白布料；在区域中心绘制圆形金色圣印：外环、内环、中央圆盘、十字光芒 |
| 腰部/饰物 | `badge`、`death_spine sash` | 金色金属/饰带 |
| 袖/手臂布层 | `top arm`、`back arm`、`back arm 1/2`、`neck top/bottom` 等 | 米白布袖，保留原绑带位置的褶皱/厚度感 |
| 手部 | `top hand`、`top hand 2`、`back hand`、`back hand 2`、`shiv_hand` | 低饱和灰白肤色 |
| 腿部/足部 | `top leg`、`bottom leg top/bottom`、`top foot`、`bottom foot`、`death_top leg`、`death_bottom leg` | 米白长袍内层/靴部布料 |
| 武器/特效 | `top dagger`、`back dagger`、`blade_shine`、`blade_twirls`、`shiv_blur` | 像素透明（区域保留，避免破坏攻击动画引用）；`slash` 改为金白圣光弧 |
| 阴影 | `shadow`、`death_shadow` | 保留原版地面阴影 |

金色纹饰只沿布料附件轮廓描边（2px），没有整张图集染白；所有区域仍保留原有透明填充、旋转标记与偏移。

## 4. 游戏内测试

- 开发测试标记：`%APPDATA%\SlayTheSpire2\believer_reskin_test.flag`（测试后删除；正常启动不显示）。
- 测试入口：`BelieverCode/Dev/SilentReskinTest.cs`，场景 `Believer/reskin_test/believer_test_visuals.tscn`；同时显示 5 份角色，分别播放 `idle_loop / attack / cast / hurt / die`。
- 日志（真实游戏进程）：
```
[BelieverReskin] native silent data: SpineSkeletonDataResource
[BelieverReskin] native skeleton_file_res=SpineSkeletonFileResource, atlas_res=SpineAtlasResource
[BelieverReskin] custom atlas load err=0
[BelieverReskin] combined data: 7 animations [attack, cast, die, hurt, idle_loop, relaxed_loop, shiv]
[BelieverReskin] idle_loop: current=idle_loop
[BelieverReskin] attack: current=attack
[BelieverReskin] cast: current=cast
[BelieverReskin] hurt: current=hurt
[BelieverReskin] die: current=die
```
- 无 missing region / UV 错误日志；截图未见明显透明缺口、错位或闪烁。
- **游戏内截图**：[docs/images/M9A2-believer-reskin-test.png](docs/images/M9A2-believer-reskin-test.png)（主菜单覆盖层，从左到右：idle_loop / attack / cast / hurt / die）。
- 观察结果：深色兜帽开口 + 金边、象牙白披风/长袍、金色圣印可见；匕首已隐藏，攻击动作仍可播放；原版 Silent 和正式信徒角色未受影响（测试代码仅在标记文件存在时激活）。

## 5. Build / Publish / 部署

- `dotnet build`：**0 warning / 0 error**
- `dotnet publish`：**exit 0**
- 部署：`Believer.dll` 481,280 B（内嵌 `believer_skin.png/.atlas`）、`Believer.pdb` 56,488 B、`Believer.pck` 234,700 B
- PCK 包含测试场景 `res://Believer/reskin_test/believer_test_visuals.tscn`；不包含源参考图/源 Silent 解密图。
- 正常启动（无测试标记）冒烟通过：M8 功能日志正常，未出现 `[BelieverReskin]`。

## 6. 已知限制（必须说明）

1. **这是程序化区域级重绘，不是最终手绘成品**。当前环境没有数字绘画工具或 AI 图像生成工具；新图集以 Silent 原始图层的 alpha/轮廓和明暗为基础，按区域重绘布料、兜帽、金边和圣印。它已经不再是简单自动调色（包含隐藏角/骨面、重画兜帽开口、圣印、金边、布料重构），但达不到真正手绘插画级质量。
2. **附件形状/网格仍由 Silent 骨架决定**：无法改变长袍宽度、袖型、兜帽轮廓、身体比例；Silent 的腿部绑带、护臂等结构仍会以米白布料形式露出，看起来更像“窄身祭司”而非参考图的宽袍大袖。
3. **部分区域仍是自动边缘金边**：不同动画下金边宽度/位置可能不完全一致；死亡/受击专用附件只做了基础处理，尚未逐个精修。
4. **武器隐藏的影响**：匕首像素透明，但攻击动画的挥击轨迹/光效仍可能存在；正式版建议将武器区域改成金色神光/圣光轨迹，而不是纯透明。
5. **未在完整战斗对局中验证**：受环境输入限制，只通过开发覆盖层验证了五种动画的实际播放与外观；未进入真实战斗检查站位、与敌人重叠、血条/意图位置等。
6. **未制作**全知全能相关：rise/float_loop/descend、七重神环、神性光球、地面圣印、1.5 秒变身演出。

## 7. 后续需要的美术工作

- 完整精修每个动画专用附件（attack/cast/hurt/die 变体、`cape_*_attack`、`death_half_scale/*`）。
- 在不改骨骼/网格的前提下，尽量用区域透明/叠加布料轮廓做出更宽的白袍下摆。
- 若宽袍大袖/全新兜帽轮廓仍不满足参考图，需要评估路线 B（重建附件/网格/骨架）：需要 Spine 编辑器或手工 Spine JSON 重建，成本明显更高。
- 正式接入信徒 `CreateCustomVisuals()` 后再做全知全能特效（独立节点，不写入角色骨架）。

## 8. 文件清单

新增/修改：
- `BelieverCode/ReskinTest/believer_skin.png`、`believer_skin.atlas`（正式换皮图集）
- `BelieverCode/ReskinTest/silent_test.*`（A1 测试图集，保留可追溯）
- `BelieverCode/ReskinTest/.gdignore`
- `BelieverCode/Dev/SilentReskinTest.cs`（dev-only 测试入口，标记启用）
- `Believer/reskin_test/believer_test_visuals.tscn`（独立测试场景）
- `Tools/SilentReskin/build_believer_skin.py`（可复现重绘脚本）
- `Assets/Character/Spine/SilentSource/silent_decoded.png`、`silent_atlas.spatlas`（源参考，不打包）
- `Assets/Reference/Believer_Concept.png`（仅恢复参考图）
- `Assets/.gdignore`、`docs/.gdignore`
- `Believer.csproj`：新增 `believer_skin.atlas/.png` 的 EmbeddedResource
- `docs/images/M9A2-believer-reskin-test.png`、本报告

未修改：卡牌、神性、全知全能、神迹、遗物、初始卡组、Silent 原版资源、正式 Believer 视觉接入代码。

本阶段完成第一版正式外观换皮并暂停；等待确认后再继续精修或接入正式角色。

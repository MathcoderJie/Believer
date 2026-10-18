# M8 信徒卡池整合与完整测试 · 检查清单与结果

> 本阶段以检查/修复为主，未新增卡牌、未调整任何卡牌数值、未制作特效。
> 判定说明：**通过（静态）** = 源码/本地化/构建级自动校验通过；**通过（构建/启动）** = Build、Publish、启动冒烟日志证据；
> **未验证** = 需要真正进入游戏操作才能确认，本报告不把它算作通过。

## 0. 本阶段发现并修复的问题

| # | 问题 | 修复 |
|---|---|---|
| 1 | 3 个旧 Power 缺少中文表（全知全能、万军降临、临时复制），zh_CN 下回退英文 | `Believer/localization/zhs/powers.json` 补齐 3 组 title/description/smartDescription（仅本地化，无行为改动） |

## 1. 40 张普通卡（自动校验，通过）

用脚本解析全部卡牌源码（`BelieverCode/Cards/Basic`、`Cards/Blue`），逐张与规格对比：

| 检查项 | 结果 | 证据 |
|---|---|---|
| 普通卡文件数量 | 通过 | 24 白 + 16 蓝 = 40 |
| 卡牌类名与预期集合一致 | 通过 | 无缺失、无多余 |
| 费用 / 类型 / 稀有度 / 目标 | 通过 | 40/40 与规格一致 |
| 基础数值（伤害/格挡/神性/抽牌/百分比等） | 通过 | 40/40 一致 |
| 升级增量（`UpgradeValueBy`） | 通过 | 40/40 一致 |
| 关键词（供奉/奉火/静祷 的消耗等） | 通过 | 40/40 一致 |
| 唯一 ID | 通过 | `BELIEVER-<ENTRY>` 全部唯一，无重复卡牌类 |
| 英文本地化（title/description） | 通过 | 40/40 存在 |
| 中文本地化（title/description） | 通过 | 40/40 存在 |
| 描述占位符与变量一致 | 通过 | `{Damage}`/`{Block}`/`{Divinity}` 等均在卡牌 DynamicVars 中定义 |
| 正常卡池归属 | 通过（静态） | 40 张均继承 `BelieverCard`（继承 `[Pool(typeof(BelieverCardPool))]`），未出现额外 `[Pool]` 属性，不会进入其他角色卡池 |
| 卡牌奖励 / 升级 / 战斗获取流程 | 未验证 | 需游戏内实际战斗奖励、营火升级验证 |

> 启动冒烟时会输出全部普通卡的占位卡图预加载日志，可作为“40 张卡已注册进卡池（`ModelDb.AllCards`）”的运行时证据（见第 5 节）。

## 2. 初始卡组与初始遗物（通过静态，运行未验证）

| 检查项 | 结果 | 证据 |
|---|---|---|
| 初始卡组总数 = 10 | 通过（静态） | `Believer.cs` 中 10 个 `ModelDb.Card<...>()` 引用 |
| 打击 ×5 / 防御 ×4 / 凝神 ×1 | 通过（静态） | 实际引用 `StrikeBeliever`×5、`DefendBeliever`×4、`Focus`×1 |
| 初始卡组不含全知全能卡 | 通过（静态） | 卡组引用中无 `OmniscienceCard` |
| 全知全能卡仍可获得 | 通过（静态） | `Mechanics/OmniscienceAvailability.cs` 在每场战斗第一个玩家回合加入手牌，不写入卡组 |
| 初始遗物只有残缺圣印且不重复 | 通过（静态） | `StartingRelics` 仅 1 个 `ModelDb.Relic<IncompleteSeal>()` |
| 新开局实际卡组/遗物 | 未验证 | 需开新游戏在牌组界面与遗物栏确认；日志会输出 `[Deck] Believer starting deck ...` |
| 旧存档不被重置 | 通过（设计） | 只修改 `StartingDeck` 声明，没有存档迁移/重置逻辑 |

## 3. 神性系统（通过代码审查，运行未验证）

| 检查项 | 结果 | 证据 |
|---|---|---|
| 上限 7 点 | 通过（静态） | `MaxAmount = 7`；`Amount` setter `Math.Clamp(value, 0, MaxAmount)` |
| 所有获取走现有资源系统 | 通过（静态） | 全部卡牌/遗物调用 `BelieverDivinityResource.Gain`（或 `Amount` clamp），无第二套神性变量 |
| 普通卡不消耗神性 | 通过（静态） | 40 张卡中无 `TrySpend`/减少神性的调用 |
| 只有全知全能消耗 7 点 | 通过（静态） | 仅 `Omniscience.Activate` 调用 `TrySpend(MaxAmount)` |
| 战斗中实际上限、获得/消耗表现 | 未验证 | 需游戏内 `divinity` 命令/HUD 验证 |

## 4. 全知全能 / 神迹 / 遗物（通过代码审查，运行未验证）

| 检查项 | 结果 | 证据 |
|---|---|---|
| 启动抽 3 张 | 通过（静态） | `CardsDrawnOnEnter = 3`；显圣追加读取 |
| 清除可清除 Debuff | 通过（静态） | `RemoveDebuffs` 按 `PowerType.Debuff` + `PowerCmd.Remove` |
| 形态期间卡牌 0 费 | 通过（静态） | `OmnisciencePower.TryModifyEnergyCostInCombat` 返回 0 |
| 形态期间不损失生命 | 通过（静态） | `ModifyHpLostAfterOsty` / `...Late` 对拥有者返回 0 |
| 持续/结束时机 | 通过（静态） | 敌方回合结束 `AfterSideTurnEnd` 移除 Power；M2 设计沿用 |
| 再创世延长一回合、只延长一次 | 通过（静态） | `RequestExtension` + `ExtensionGranted` 双标记；`_extensionRequested` 消耗一次后不再延长 |
| 延长不重复启动效果 | 通过（静态） | 延长分支只保留/保留手牌神迹，不重新抽牌、不清 Debuff、不重新发放神迹 |
| 显圣不因延长重复触发 | 通过（静态） | 显圣只在 `Omniscience.Activate` 读取追加抽牌，延长不执行启动序列 |
| 12 张神迹存在且仅形态内可用 | 通过（静态） | 12 个 `MiracleCard` 子类；`MiracleCard.IsPlayable` 要求 `OmnisciencePower` |
| 神迹不进永久卡组 | 通过（静态） | 神迹通过 `CreateCard` 生成战斗实例，无任何入卡组路径 |
| 形态结束清理未使用神迹 | 通过（静态） | `BeforeSideTurnEnd` + `AfterRemoved` → `CleanUpMiracles`（覆盖手牌/抽牌/弃牌/消耗/Play 区） |
| 复制/重复事件不会无限生成 | 通过（静态） | `MiraclesGranted` 一次性生成标记 + `MiracleCatalog`/收藏去重 |
| 神迹异常 ID 安全处理 | 通过（静态） | 未解析 ID 跳过并告警；重复卡牌非法拒绝 |
| 残缺圣印回合开始触发 | 通过（静态） | `IncompleteSeal.AfterPlayerTurnStart`，`player != Owner` 守卫 |
| 神性满时不溢出 | 通过（静态） | 遗物 `Gain` 受 `Amount` clamp 7 限制 |
| 切战斗/节点错误触发 | 通过（静态） | `AfterPlayerTurnStart` 是战斗内钩子，不会在地图/奖励界面触发 |
| 以上全部实际战斗表现 | 未验证 | 需游戏内逐项验证（见第 7 节） |

## 5. 构建 / 部署 / 启动冒烟

- `dotnet build Believer.csproj`：C# 编译 **0 warning / 0 error**（仅当游戏正在运行时会卡在“复制 dll 到 mods”步骤）。
- `dotnet publish ... -p:PublishProfile=FolderProfile`：**exit 0**。
- 最终部署（`D:\1\steamapps\common\Slay the Spire 2\mods\Believer\`）：
  `Believer.dll` 132,096 B（11:34:15）、`Believer.pdb` 55,412 B、`Believer.pck` 233,164 B（11:34:19）。
- 启动冒烟（zh_CN，最终构建）：
  - mod 初始化成功；`[Patches] Divinity gain observer: prefix=1, postfix=1.`；
  - `[Omniscience] Ability card provider installed (...)`；
  - **40 张普通卡占位卡图全部出现在预加载日志**（模型已注册进卡池 / `ModelDb.AllCards`）；
  - `zhs cards.json` / `zhs powers.json` 加载合并，无本地化格式化错误、无异常。

## 6. 尚需人工验证清单（游戏内）

1. 新游戏选信徒：牌组 10 张（打击5/防御4/凝神1），遗物栏只有残缺圣印；日志 `[Deck] Believer starting deck ...` 与之一致。
2. 战斗第 1 回合手牌出现全知全能卡（不在牌组）；凝神能 +神性并抽牌。
3. 残缺圣印每回合开始 +1 神性；神性到 7 后不再增加（不溢出）。
4. 神性相关条件卡（坚守/奉献/神恩/沉着/祷告/神罚/圣裁/献祭/代价/破限）在阈值前后效果正确。
5. 全知全能：抽 3、清 Debuff、费用 0、免伤、敌方回合结束退出；再创世延长一次且不会重复启动；显圣只额外抽一次。
6. 12 张神迹：只在形态内可用，效果正常，形态结束未用神迹清理干净，不进入牌组。
7. 战斗奖励/商店/图鉴：能看到并升级这 40 张信徒卡；其他角色初始卡组/卡池不受影响。
8. 旧存档读档不被重置。

## 7. 已知限制（不影响本阶段验收，但需知悉）

- 40 张普通卡及本阶段涉及的 Power 已有中英文文本；**M1–M6 的角色名/遗物/神迹等旧文本中文本地化仍缺失**，zh_CN 下回退英文（本次未改，属历史约定）。
- 卡图为占位图，无专属动画/特效（视觉阶段未开始）。
- 回响的“原始伤害”取 `Damage` 变量，适用于本 mod 全部攻击牌；未来新增 Calculated/Osty 伤害攻击牌需扩展。
- 白座终审的 Boss 判定基于遭遇房间类型（游戏无单怪 Boss 标记），Boss 房小怪按 Boss 处理。
- 通过仅代表静态/构建/启动级证据；战斗内行为需按第 6 节人工验证。

# M7 第四、五批：16 张蓝卡 · 实现说明

> 本文件覆盖第四批（8 张蓝卡）与第五批（8 张蓝卡）。至此 40 张普通卡全部实现（24 白 + 16 蓝）。
> 前三批白卡与 M1–M6 系统未改动（除显圣需要的一处全知全能启动读取）。

## 1. 卡牌清单（全部 `CardRarity.Uncommon` 蓝卡）

### 第四批

| 卡牌 | ID（控制台用） | 费用/类型 | 效果 | 升级 |
|---|---|---:|---|---|
| 神罚 | `BELIEVER-DIVINE_PUNISHMENT` | 1/攻击 | 单体 12；神性≥4 时再全体 8 | 15 / 10 |
| 圣裁 | `BELIEVER-SACRED_JUDGMENT` | 2/攻击 | 单体 18；神性≥5 时额外 15 | 22 / 18 |
| 裁决 | `BELIEVER-VERDICT` | 1/攻击 | 12；目标每有 1 种负面状态额外 4，上限 8 | 15 / 每种 5 / 上限 10 |
| 连环 | `BELIEVER-CHAIN` | 1/攻击 | 4 次 × 4 | 每次 5 |
| 终结 | `BELIEVER-FINALITY` | 2/攻击 | 24；目标 HP≤50% 时额外 12 | 30 / 15 |
| 奉火 | `BELIEVER-FIRE_OFFERING` | 1/技能 | +3 神性，然后消耗 | +4 神性 |
| 代价 | `BELIEVER-PRICE` | 0/技能 | 抽 2；神性≥4 时 +1 临时能量 | 抽 3 |
| 献祭 | `BELIEVER-SACRIFICE` | 1/技能 | 全体 12；神性≥5 时再全体 12 | 15 / 15 |

### 第五批

| 卡牌 | ID（控制台用） | 费用/类型 | 效果 | 升级 |
|---|---|---:|---|---|
| 赦免 | `BELIEVER-ABSOLUTION` | 1/技能 | 清除自身所有可清除负面状态，+8 格挡 | +12 格挡 |
| 预言 | `BELIEVER-PROPHECY` | 1/技能 | 抽 3、弃 1、+1 神性 | 抽 4 |
| 回响 | `BELIEVER-ECHO` | 1/技能 | 本回合下一张攻击牌额外造成其原始伤害 50% 的伤害 | 75% |
| 破限 | `BELIEVER-LIMIT_BREAK` | 1/技能 | +1 临时能量；神性≥5 时额外抽 2 | 额外抽 3 |
| 神临 | `BELIEVER-THEOPHANY` | 2/技能 | +3 神性、15 格挡、抽 1 | +4 神性、18 格挡 |
| 圣约 | `BELIEVER-COVENANT` | 1/能力 | 每回合首次获得神性时额外 +1 神性 | 触发时再 +3 格挡 |
| 显圣 | `BELIEVER-MANIFESTATION` | 2/能力 | 每次进入全知全能形态时额外抽 2 | 额外抽 3 |
| 聚能 | `BELIEVER-CHANNEL` | 1/能力 | 每回合每打出 3 张牌 → +1 格挡并抽 1 张，最多 3 次 | 每 2 张触发 |

新增 Power：`EchoPower`、`CovenantPower`、`CovenantPowerBlessed`、`ManifestationPower`、`ChannelPower`
（每个均有 power 本地化）。

## 2. 关键机制实现

| 需求 | 实现 |
|---|---|
| 临时能量 | `PlayerCmd.GainEnergy(1m, owner)`（原版能量获取 API，只影响当前回合能量） |
| 抽牌后弃牌 | `CardPileCmd.Draw` → `CardSelectCmd.FromHandForDiscard(DiscardSelectionPrompt, 1)` → `CardCmd.Discard`（与第一/二批的预备同一写法） |
| 负面状态计数（裁决） | 统计目标 `IsVisible && TypeForCurrentAmount == PowerType.Debuff` 的 Power 数量，额外伤害 = min(每种×数量, 上限) |
| 清除负面状态（赦免） | 与 M2/净化同一判定（可见 Debuff），全部经 `PowerCmd.Remove` 移除；隐藏内部 Power 与 Buff 不动 |
| 回响不重复触发 | `EchoPower.AfterCardPlayed` 在下一张攻击牌结算后，用其 `DynamicVars.Damage.BaseValue × 百分比` 造成一次独立伤害（单体→同目标；AoE→所有存活敌人），**不重跑卡牌 OnPlay**，因此不会重复该牌其它效果；未用掉时回合结束移除 |
| 圣约不无限循环 | 复用 M3 批次的 `DivinityGainTracker`：额外获得的神性会被计数但**不再派发监听事件**，因此圣约不会重复触发自身；升级版 `CovenantPowerBlessed` 在触发时额外 +3 格挡 |
| 显圣不因延长重复触发 | 在全知全能启动序列读取 `ManifestationPower.Amount` 追加抽牌；再创世延长形态不会重新执行启动序列，也不会重新施加该 Power，所以只触发一次 |
| 聚能计数 | `ChannelPower` 按 `PlayerCombatState.TurnNumber` 惰性重置计数；打出聚能本身不计入（`Configure` 设置跳过下一次出牌）；每达到阈值 → `GainBlock(1, Unpowered)` + `Draw(1)`，每回合最多 3 次 |
| 神性上限/不消耗 | 所有获得走 `BelieverDivinityResource.Gain`（clamp 0–7）；所有条件只读 `Amount`，普通卡不消耗神性 |

## 3. 本地化

- `eng/cards.json` + `zhs/cards.json`：16 张蓝卡英文/中文名称与描述。
- `eng/powers.json` + `zhs/powers.json`：5 个新 Power 的 title/description/smartDescription。

## 4. 新增 / 修改文件

新增（16 卡 + 5 Power）：

```
BelieverCode/Cards/Blue/*.cs（16 个，含 .uid）
  DivinePunishment / SacredJudgment / Verdict / Chain / Finality / FireOffering / Price / Sacrifice
  Absolution / Prophecy / Echo / LimitBreak / Theophany / Covenant / Manifestation / Channel
BelieverCode/Powers/Blue/*.cs（5 个，含 .uid）
  EchoPower / CovenantPower / CovenantPowerBlessed / ManifestationPower / ChannelPower
docs/M7-BlueCards.md
```

修改：

```
Believer/localization/eng/cards.json / zhs/cards.json      16 张蓝卡文本
Believer/localization/eng/powers.json / zhs/powers.json     5 个 Power 文本
BelieverCode/Mechanics/Omniscience.cs                       显圣：启动抽牌数 + ManifestationPower.Amount
```

未改动：前 24 张白卡、M1 神性资源、M2 全知全能核心、M3 遗物、M4–M6 神迹系统。

## 5. Build / Publish / 部署结果

- `dotnet build Believer.csproj`：成功，**0 warning / 0 error**。
- `dotnet publish ... -p:PublishProfile=FolderProfile`：**exit 0**。
- 部署：`Believer.dll` 129,536 B、`Believer.pdb` 54,712 B、`Believer.pck` 232,000 B（11:16）。
- 启动冒烟（zh_CN）：
  - 16 张蓝卡占位卡图全部出现在预加载日志（登记模型成功）；
  - `zhs cards.json` / `zhs powers.json` 加载合并；
  - `[Patches] Divinity gain observer: prefix=1, postfix=1.`（圣约/信标/施予所依赖的神性追踪在岗）；
  - 无异常、无本地化格式化错误。

## 6. 游戏内测试步骤（蓝卡 16 张）

前置：mod 模式进入「信徒」战斗；控制台 `card <ID>`、`upgrade <序号>`、`divinity <show|set|add|full|empty>`、
`power <PowerID> <层数> <目标索引>`。

1. 神罚：目标单体 12；`divinity set 3` → 无群伤；`divinity set 4` → 12 + 全体 8；升级 15/10。
2. 圣裁：单体 18；`divinity set 4` → 无额外；`divinity set 5` → 18+15；升级 22/18。
3. 裁决：目标无 Debuff → 仅 12；`power VULNERABLE_POWER 1 1`（给敌人上 1 种）→ 12+4；上 2 种 → +8；
   上 3 种 → 仍 +8（上限）；升级 15 / 每种 5 / 上限 10。
4. 连环：4 段、每段 4（升级 5）。
5. 终结：目标 >50% HP → 24；≤50% → 24+12；升级 30/15。
6. 奉火：1 费 +3 神性并进消耗堆；升级 +4。
7. 代价：抽 2；`divinity set 3` → 无能量；`divinity set 4` → 抽 2 +1 能量；升级抽 3。
8. 献祭：全体 12；`divinity set 4` → 无额外；`divinity set 5` → 12+12；升级 15/15。
9. 赦免：用 `power VULNERABLE_POWER` / `WEAK_POWER` 上多种负面状态 → 全部清除、+8 格挡（升级 12）；
   Buff/隐藏状态不被清除。
10. 预言：抽 3 → 弃 1 → +1 神性；升级抽 4、弃 1、+1。
11. 回响：打出回响后打一张攻击牌（如打击/连环）→ 攻击后额外造成其原始伤害 50% 的伤害；
    升级 75%；不重复触发该攻击牌的其它效果；不打攻击牌则回合结束失效。
12. 破限：+1 能量；`divinity set 4` → 不抽；`divinity set 5` → 额外抽 2（升级 3）。
13. 神临：+3 神性、15 格挡、抽 1；升级 +4 / 18 / 抽 1。
14. 圣约：打出后本回合首次获得神性 → 额外 +1 神性（升级版再 +3 格挡）；确认不会无限循环；
    每回合只触发一次。
15. 显圣：打出后进入全知全能 → 抽牌数 = 3 + 2（升级 3+3）；用再创世延长形态 → **不会**再次额外抽牌；
    形态结束再进一次 → 再次生效。
16. 聚能：打出后打 3 张牌 → +1 格挡并抽 1 张（每 3 张一次，最多 3 次/回合）；升级每 2 张一次；
    回合结束后计数重置。
17. 全卡池：正常战斗奖励中确认 16 张蓝卡可出现；图鉴确认中文描述与升级数值一致。

## 7. 已知限制

- 卡图仍为占位图，无专属动画（后续视觉阶段）。
- 回响的“原始伤害”取卡牌 `Damage` 变量的基础值；本 mod 所有攻击牌都使用该变量。若未来加入
  CalculatedDamage/OstyDamage 类攻击牌，需要扩展回响的取值逻辑。
- 圣约的额外神性会被 `DivinityGainTracker` 计数但不派发监听；与施予/信标同时存在时，嵌套获得的计数
  可能让“前两次获得”的判定少一次（不会造成循环或崩溃）。
- 聚能 Power 描述不写具体张数（阈值由卡牌定义），卡牌描述是准确的；重复打出第二张聚能会刷新阈值与计数。
- 中文表覆盖全部 40 张普通卡与相关 Power；M1–M6 旧卡仍按机制回退英文。

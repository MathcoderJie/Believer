# M7 第二批：信徒的 8 张白卡 · 实现说明

> 本阶段只实现以下 8 张普通（白）卡。第一批 8 张保持不变；其余卡牌未实现。
> 不修改神性、全知全能、初始遗物、神迹系统。

## 1. 卡牌清单

全部 `CardRarity.Common`，继承 `BelieverCard`（继承 `[Pool(typeof(BelieverCardPool))]`），
自动加入信徒正常卡池（战斗奖励、商店、随机生成、图鉴）。

| 卡牌 | 卡牌 ID（控制台用） | 费用 | 基础效果 | 升级效果 |
|---|---|---:|---|---|
| 供奉 | `BELIEVER-FAITH_OFFERING` | 0 | 获得 1 神性，然后消耗此牌 | 获得 2 神性 |
| 静心 | `BELIEVER-INNER_CALM` | 1 | 获得 1 神性和 5 格挡 | 格挡 7 |
| 奉献 | `BELIEVER-DEDICATION` | 1 | 10 格挡；若神性 ≥ 3，额外 6 格挡 | 12 格挡 / 额外 8 |
| 净化 | `BELIEVER-PURIFICATION` | 1 | 清除自身 1 个可清除负面状态，获得 5 格挡 | 格挡 8 |
| 预备 | `BELIEVER-PREPARATION` | 0 | 抽 1 张牌，然后弃 1 张牌 | 抽 2 张，再弃 1 张 |
| 祷告 | `BELIEVER-PRAYER` | 1 | 抽 2 张牌；若神性 ≥ 3，获得 1 神性 | 抽 3 张 |
| 神恩 | `BELIEVER-DIVINE_GRACE` | 1 | 7 格挡；若神性 ≥ 1，额外 3 格挡 | 10 格挡 / 额外 4 |
| 沉着 | `BELIEVER-COMPOSURE` | 1 | 6 格挡；若神性为 0，获得 1 神性 | 格挡 9 |

## 2. 实现要点（复用第一批已验证的写法）

| 需求 | 实际实现 |
|---|---|
| 神性上限 / 不消耗神性 | 全部通过 M1 的 `BelieverDivinityResource.Gain`（内部 clamp 0..7）；本批卡牌都不调用 `TrySpend`，只有条件读取 `Amount` |
| 数值与升级一致 | `DynamicVars`（`BlockVar` / `CardsVar` / 自定义 `DynamicVar("Divinity"/"ExtraBlock")`）+ `OnUpgrade` 的 `UpgradeValueBy`；描述占位符 `{Block:diff()}` 等由引擎自动填值 |
| 供奉消耗 | `CanonicalKeywords => [CardKeyword.Exhaust]`，引擎在效果结算后把牌放入消耗堆 |
| 预备先抽后弃 | 与游戏原版 `Prepared` 相同：`CardPileCmd.Draw` → `CardSelectCmd.FromHandForDiscard(DiscardSelectionPrompt, 1)` → `CardCmd.Discard`；升级只提高 `CardsVar`（抽牌数），弃牌数固定 1 |
| 祷告条件 | 先抽牌，再读取 `BelieverDivinityResource.Amount >= 3` 决定是否 `Gain(1)` |
| 沉着条件 | 先获得格挡，再读取 `Amount == 0` 决定是否 `Gain(1)` |
| 奉献 / 神恩条件 | 打出时读取当前神性（≥3 / ≥1），只判断不消耗 |
| 净化 | 见下方“净化实现说明” |

### 净化实现说明（重要）

排查结论：**本机 STS2 没有“清除负面状态”的现成 API，也没有 Power 级的“不可清除”标记**
（`PowerModel` 只有 `PowerType`；全代码检索无 `RemoveDebuff` / `PowerSelectCmd` / 不可清除标记）。
可用的游戏机制是：

1. 负面状态 = `PowerModel.TypeForCurrentAmount == PowerType.Debuff`（M2 全知全能清 Debuff 采用同一判定）；
2. 移除必须走 `PowerCmd.Remove`（唯一会执行 `AfterRemoved` 的移除路径）；
3. 不可清除/内部状态 = **不可见 Power**（`IsVisible == false`，例如隐藏的记账 Power），因此只遍历
   `IsVisible == true` 的 Debuff，绝不触碰隐藏内部状态或 Buff。

当存在多个可清除 Debuff 时，按“层数最高优先，层数相同按 ID 排序”稳定选择一个（保证多端一致），
然后正常获得格挡。若没有可清除状态，则只获得格挡。

## 3. 本地化

- `Believer/localization/eng/cards.json`：新增 8 张英文文本。
- `Believer/localization/zhs/cards.json`：新增 8 张中文名称/描述（zh_CN 下显示中文）。

## 4. 新增 / 修改文件

新增：

```
BelieverCode/Cards/Basic/FaithOffering.cs     供奉
BelieverCode/Cards/Basic/InnerCalm.cs         静心
BelieverCode/Cards/Basic/Dedication.cs        奉献
BelieverCode/Cards/Basic/Purification.cs      净化
BelieverCode/Cards/Basic/Preparation.cs       预备
BelieverCode/Cards/Basic/Prayer.cs            祷告
BelieverCode/Cards/Basic/DivineGrace.cs       神恩
BelieverCode/Cards/Basic/Composure.cs         沉着
（各带 .uid 文件）
docs/M7-BasicCards2.md                        本文件
```

修改：

```
Believer/localization/eng/cards.json           新增 8 张英文文本
Believer/localization/zhs/cards.json           新增 8 张中文文本
```

未改动：第一批 8 张卡、M1–M6 全部系统。

## 5. Build / Publish / 部署结果

- `dotnet build Believer.csproj`：成功，**0 warning / 0 error**。
- `dotnet publish Believer.csproj -p:PublishProfile=FolderProfile`：**exit 0**。
- 部署（`D:\1\steamapps\common\Slay the Spire 2\mods\Believer\`）：
  `Believer.dll` 97,792 B（11:00:22）、`Believer.pdb` 42,608 B、`Believer.pck` 212,884 B（11:00:26）。
- 启动冒烟（zh_CN）：
  - mod 初始化成功、无异常；
  - `zhs cards.json` 加载合并；
  - 8 张新卡的占位卡图路径全部出现在日志（`composure` / `dedication` / `divine_grace` /
    `faith_offering` / `inner_calm` / `prayer` / `preparation` / `purification`），说明 8 个模型已注册；
  - 无本地化格式化错误。

## 6. 游戏内测试步骤

前置：mod 模式进入「信徒」的一局，进战斗后开控制台。用 `card <ID>` 生成到手牌，`upgrade <手牌序号>` 升级，
`divinity <show|set|add|full|empty>` 控制神性，`power <PowerId> <层数> <目标索引>` 施加状态
（目标索引来自 `CombatManager.DebugOnlyGetState().Creatures`，玩家通常是 0）。

1. **供奉**：`card BELIEVER-FAITH_OFFERING` → 打出后神性 +1，卡进入消耗堆；升级后 +2，仍进消耗堆。
2. **静心**：1 费 → +1 神性、+5 格挡；升级后 +1 神性、+7 格挡。
3. **奉献**：`divinity set 2` 打出 → 仅 +10 格挡；`divinity set 3` 打出 → +16 格挡；
   升级后为 +12 / +20；全程神性不变。
4. **净化**：
   - `power VULNERABLE_POWER 2 0`（给玩家上易伤）→ 打出净化 → 易伤被移除，获得 5 格挡（升级 8）；
   - 无负面状态时打出 → 只获得格挡；
   - 同时有多个 Debuff 时只移除 1 个（层数最高者）；
   - 确认 Buff（如力量）与隐藏内部状态不被移除。
5. **预备**：0 费 → 先抽 1 张，然后弹出弃牌选择并弃 1 张；升级后抽 2 张、弃 1 张（顺序不可颠倒）。
6. **祷告**：`divinity set 2` 打出 → 只抽 2 张，不加神性；`divinity set 3` 打出 → 抽 2 张且 +1 神性；
   升级后抽 3 张，神性条件不变。
7. **神恩**：`divinity set 0` 打出 → 仅 7 格挡；`divinity set 1` 打出 → 7+3=10 格挡；升级后 10 / 14。
8. **沉着**：`divinity set 0` 打出 → 6 格挡且 +1 神性；`divinity set 1` 打出 → 仅 6 格挡；升级后 9 格挡。
9. **上限**：`divinity set 7` 后打出供奉/静心/祷告/沉着 → 保持 7/7，不会超过上限。
10. **卡池**：正常推图/战斗奖励中确认能看到这两批 16 张白卡；图鉴中确认中文名称与描述。

## 7. 已知限制

- 卡图仍为占位图，无专属动画/特效（后续阶段）。
- 净化为“自动选择 1 个可清除负面状态”（游戏没有 Power 选择 UI/不可清除标记），选择规则见第 2 节；
  如果游戏后续版本提供“不可清除”标记，应改用该标记。
- 中文表只覆盖已实现的白卡（前 16 张），其余卡牌按机制回退英文，可后续补齐。
- 本批只实现 8 张，未继续第三批。

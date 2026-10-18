# M7 第一批：信徒的 8 张白卡 · 实现说明

> 本阶段只实现以下 8 张普通（白）卡并接入信徒正常卡池。其余 32 张未实现，等确认后再继续。
> 不修改 M1 神性系统、M2 全知全能、M3 初始遗物、12 张神迹。

## 1. 卡牌清单

所有卡牌都是 `CardRarity.Common`，继承 `BelieverCard`（自带 `[Pool(typeof(BelieverCardPool))]`，
因此自动加入信徒正常卡池：结算奖励、商店、随机生成都会出现，同时会进卡牌图鉴）。

| 卡牌 | 卡牌 ID（控制台用） | 费用 | 类型/目标 | 基础效果 | 升级效果 |
|---|---|---:|---|---|---|
| 打击 | `BELIEVER-STRIKE_BELIEVER` | 1 | 攻击 / 单体 | 造成 6 点伤害 | 9 点（+3） |
| 重击 | `BELIEVER-HEAVY_STRIKE` | 2 | 攻击 / 单体 | 造成 15 点伤害 | 20 点（+5） |
| 连斩 | `BELIEVER-TRIPLE_SLASH` | 1 | 攻击 / 单体 | 3 次、每次 3 点伤害 | 每次 4 点（+1） |
| 横扫 | `BELIEVER-SWEEP` | 1 | 攻击 / 全体 | 对所有敌人造成 6 点伤害 | 9 点（+3） |
| 防御 | `BELIEVER-DEFEND_BELIEVER` | 1 | 技能 / 自身 | 获得 5 点格挡 | 8 点（+3） |
| 坚守 | `BELIEVER-STEADFAST` | 1 | 技能 / 自身 | 获得 8 点格挡；若当前神性 ≥ 3，额外获得 4 点格挡 | 10 点基础、5 点额外 |
| 凝神 | `BELIEVER-FOCUS` | 1 | 技能 / 自身 | 获得 1 点神性，然后抽 1 张牌 | 获得 2 点神性，抽 1 张牌 |
| 静祷 | `BELIEVER-SILENT_PRAYER` | 1 | 技能 / 自身 | 获得 2 点神性，然后消耗此牌 | 获得 3 点神性，然后消耗此牌 |

## 2. 实现方式（全部使用项目现有代码 + 实际可用的 STS2 API）

| 需求 | 实际实现 |
|---|---|
| 接入信徒卡池 | 继承 `BelieverCard`（BaseLib 的 `PoolAttribute` 为 `Inherited = true`），自动加入 `BelieverCardPool` |
| 数值与升级 | `DynamicVars`：`DamageVar` / `BlockVar` / `new DynamicVar("Divinity" / "ExtraBlock", value)`；`OnUpgrade` 用 `UpgradeValueBy` 修改同一变量，描述与实际效果自动同步 |
| 描述中的数值 | 本地化占位符 `{Damage:diff()}`、`{Block:diff()}`、`{Divinity:diff()}`、`{ExtraBlock:diff()}`；`CardModel.GetDescriptionForPile` 会自动把 `DynamicVars` 填入 `LocString`，`diff()` 会高亮升级差值 |
| 伤害 | `DamageCmd.Attack(...).FromCard(this).Targeting(cardPlay.Target)`（单体）/ `.TargetingAllOpponents(CombatState)`（横扫） |
| 多段独立结算 | 连斩用 `DamageCmd.Attack(3m).WithHitCount(3)`，引擎每次命中是一个独立的 `CreatureCmd.Damage` 结算 |
| 格挡 | `CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay)` |
| 神性读取 | `Owner.GetResource<BelieverDivinityResource>()`（M1 资源，BaseLib 扩展方法），只读 `Amount` |
| 神性获得 | 同一资源实例的 `Gain(n)`（内部 `Amount` setter 会 clamp 到 0..7，沿用 M1 实现，没有新建变量） |
| 凝神顺序 | 先 `Gain(...)`，再 `CardPileCmd.Draw(choiceContext, 1, Owner)` |
| 静祷消耗 | `CanonicalKeywords => [CardKeyword.Exhaust]`，引擎在卡牌效果结算后把牌放入消耗堆（游戏规定的消耗区域） |
| 坚守条件 | 打出时读取当前 `BelieverDivinityResource.Amount >= 3`，不消耗神性 |
| 普通卡不消耗神性 | 8 张卡均不调用 `TrySpend`/减少神性，只读取或 `Gain` |

## 3. 本地化

- `Believer/localization/eng/cards.json`：新增 8 张卡的英文 `title` / `description`。
- `Believer/localization/zhs/cards.json`（**本阶段新增中文表**）：8 张卡的中文名称与描述。
  游戏语言为简体中文（zh_CN）时，BaseLib 会加载 mod 的 zhs 表并覆盖英文；本 mod 其他旧卡牌在 zhs 表缺失的
  键会按游戏机制回退到 eng 表，因此不会丢失文本。

## 4. 新增 / 修改文件

新增：

```
BelieverCode/Cards/Basic/StrikeBeliever.cs      打击
BelieverCode/Cards/Basic/HeavyStrike.cs         重击
BelieverCode/Cards/Basic/TripleSlash.cs         连斩
BelieverCode/Cards/Basic/Sweep.cs               横扫
BelieverCode/Cards/Basic/DefendBeliever.cs      防御
BelieverCode/Cards/Basic/Steadfast.cs           坚守
BelieverCode/Cards/Basic/Focus.cs               凝神
BelieverCode/Cards/Basic/SilentPrayer.cs        静祷
（以上各带 .uid 文件）
Believer/localization/zhs/cards.json            中文卡牌表
docs/M7-BasicCards.md                           本文件
```

修改：

```
Believer/localization/eng/cards.json            新增 8 张卡的英文文本
```

未改动：M1 神性资源代码、M2 全知全能、M3 遗物、M4/M5/M6 神迹与收藏、角色/卡池/遗物池代码。

## 5. Build / Publish / 部署结果

- `dotnet clean` → `dotnet build Believer.csproj`：成功，**0 warning / 0 error**。
- `dotnet publish Believer.csproj -p:PublishProfile=FolderProfile`：**exit 0**。
- 部署（`D:\1\steamapps\common\Slay the Spire 2\mods\Believer\`）：
  `Believer.dll` 90,112 B（10:50:45）、`Believer.pdb` 39,936 B、`Believer.pck` 209,028 B（10:50:49）。
- 启动冒烟（zh_CN 语言）：
  - mod 初始化成功，无异常、无 Harmony 失败；
  - `zhs cards.json` 被加载并合并（`Found loc table from mod: zhs cards.json`）；
  - 8 张新卡的卡图占位路径全部出现在日志中（`defend_believer.png` / `focus.png` /
    `heavy_strike.png` / `silent_prayer.png` / `steadfast.png` / `strike_believer.png` /
    `sweep.png` / `triple_slash.png`），说明 8 个卡牌模型已注册并在预加载；
  - 无本地化格式化错误（`Localization formatting error` 未出现）。

> 说明：卡图目前使用占位图（与之前卡牌一致）；本阶段不做专属动画/视觉特效。

## 6. 游戏内测试步骤

前置：mod 模式进入「信徒」的一局，进战斗后打开开发者控制台。卡牌本身在正常卡池里，可直接用控制台
`card <卡牌ID>` 生成到手牌；`upgrade <手牌序号>` 可升级手牌中的卡（0 = 最左边）。

1. **获取与使用**
   - `card BELIEVER-STRIKE_BELIEVER` → 手牌出现「打击」，打出对敌人造成 6 点伤害。
   - `card BELIEVER-HEAVY_STRIKE` → 2 费，造成 15 点伤害。
   - `card BELIEVER-TRIPLE_SLASH` → 3 段、每段 3 点（观察 3 次独立伤害数字）。
   - `card BELIEVER-SWEEP` → 对所有敌人各造成 6 点伤害（多敌人战斗时验证）。
   - `card BELIEVER-DEFEND_BELIEVER` → 获得 5 点格挡（HUD 格挡 +5）。
2. **升级与描述**
   - `card BELIEVER-STRIKE_BELIEVER`，`upgrade 0` → 卡面显示 9，打出造成 9 点。
   - 同样验证重击 20、连斩每段 4、横扫 9、防御 8、坚守 10/5、凝神 2 神性、静祷 3 神性。
   - 升级预览（图鉴/卡牌升级 UI）中绿色差值应正确。
3. **神性上限**
   - `divinity set 6` 后打出凝神 → 神性变为 7（而不是 8），再次打出/连续获得仍不超过 7。
   - `divinity full` 后打出凝神/静祷 → 保持 7/7。
4. **坚守条件**
   - `divinity set 2` 打出坚守 → 只获得 8 格挡；`divinity set 3` 再打出 → 共 12 格挡（8+4）；
   - 升级后对应 10 / 10+5=15；全程神性数值不变（不消耗）。
5. **凝神顺序**
   - 手牌满或接近满时使用，确认先加神性再抽牌；`divinity` 命令可观察数值。
6. **静祷消耗**
   - 打出静祷 → 神性 +2（升级 +3），卡牌进入消耗堆（消耗堆计数 +1），不会洗回抽牌堆。
7. **卡池接入**
   - 正常推图/触发战斗胜利结算，确认战斗奖励中能看到信徒白卡（卡池已接入）；
   - 或在图鉴中确认 8 张卡存在且描述为中文。
8. **普通卡不消耗神性**：打出以上任意卡后，神性数值只会因凝神/静祷增加，绝不减少。

## 7. 已知限制

- 8 张卡暂无专属卡图（占位图）与专属动画，视觉属于后续阶段。
- 中文文本目前只覆盖本阶段 8 张卡；M1–M6 的旧卡牌在 zhs 表缺失时回退英文（可后续补齐中文表）。
- 未实现本批之外的其他 32 张卡，等待确认后再继续。

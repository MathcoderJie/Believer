# M7 第三批：8 张白卡（含 4 张能力牌）· 实现说明

> 本批只实现 8 张白卡，前 16 张白卡与 M1–M6 系统保持不变。第四批（8 蓝）、第五批（8 蓝）尚未开始。

## 1. 卡牌清单

全部 `CardRarity.Common`，继承 `BelieverCard`（自动进入信徒正常卡池）。

| 卡牌 | ID（控制台用） | 费用 | 类型 | 效果 | 升级 |
|---|---|---:|---|---|---|
| 破甲 | `BELIEVER-ARMOR_BREAK` | 1 | 攻击 | 8 伤害；目标有格挡时额外 4 | 10 / 额外 5 |
| 追击 | `BELIEVER-PURSUIT` | 0 | 攻击 | 4 伤害；本回合曾获得神性则额外 4 | 6 / 额外 4 |
| 穿刺 | `BELIEVER-PIERCE` | 1 | 攻击 | 9 伤害 | 12 |
| 斩落 | `BELIEVER-SEVERING_STRIKE` | 1 | 攻击 | 8 伤害；目标 HP ≤ 50% 时额外 5 | 11 / 额外 7 |
| 信标 | `BELIEVER-BEACON` | 1 | 能力 | 每回合首次获得神性时获得 3 格挡 | 5 格挡 |
| 守护 | `BELIEVER-GUARDIAN` | 1 | 能力 | 每回合结束时若神性≥1，获得 5 格挡 | 7 格挡 |
| 施予 | `BELIEVER-BESTOW` | 1 | 能力 | 每回合前两次获得神性时，每次获得 3 格挡 | 4 格挡 |
| 专注 | `BELIEVER-CONCENTRATION` | 1 | 能力 | 每回合首次打出技能牌时，获得 1 神性 | 2 神性 |

对应 4 个 Power：`BeaconPower`、`GuardianPower`、`BestowPower`、`ConcentrationPower`。

## 2. 新增机制：神性获取事件追踪

追击 / 信标 / 施予 / 专注 需要知道“本回合获得了几次神性”。检查结论：BaseLib 只有**消耗**自定义资源的钩子
（`BaseLibHooks.AfterSpendCustomResource`），没有“获得资源”的钩子；M1 资源本身也不允许改。

因此新增：

1. `BelieverCode/Mechanics/DivinityGainTracker.cs`
   - 按 `PlayerCombatState` 记录 `TurnNumber` 与本回合获得次数（按玩家回合重置，战斗结束自动回收）；
   - 提供 `GainCountThisTurn` / `HasGainedThisTurn`（追击使用）；
   - 提供按战斗状态注册的监听器（信标/施予使用）；
   - 嵌套获得（监听器在响应中获得更多神性，例如第五批的圣约）会被计数但不会再次派发监听事件，
     从机制上避免“额外获得的神性重复触发”形成的无限循环。
2. `BelieverCode/Patches/DivinityGainTrackingPatch.cs`
   - 只在 `BelieverDivinityResource.Gain` 上加 prefix/postfix，读取调用前后 Amount 的差值；
     **不改变 M1 的任何行为**，被 7 点上限截断的“假获得”不会计数；
   - 所有获得途径（M3 遗物的回合开始 +1、本 mod 卡牌、未来效果）都走这个 M1 API，因此追踪完整。
   - 启动日志自检：`[Patches] Divinity gain observer: prefix=1, postfix=1.`（已实测输出）。

能力牌行为：
- **信标**：监听本回合第 1 次获得 → `GainBlock(Amount, Unpowered)`（与游戏原版 After Image 的
  Power 格挡同一属性），只触发一次/回合。
- **施予**：监听本回合第 1、2 次获得 → 每次 `GainBlock(Amount)`，第 3 次起不再触发。
- **守护**：`BeforeSideTurnEnd`（玩家侧、拥有者参与）检查 `BelieverDivinityResource.Amount >= 1`
  → `GainBlock(Amount)`；只读神性，不消耗。
- **专注**：`AfterCardPlayed` 中按 `PlayerCombatState.TurnNumber` 惰性重置“本回合已触发”标记，
  首次打出 `CardType.Skill` 时 `Gain(Amount)`（走 M1 API，受 7 点上限约束）。

## 3. 攻击牌实现

- 破甲：先记录 `target.Block > 0`，再打基础伤害，条件满足再打额外伤害（即使基础伤害打空格挡也算）。
- 追击：`DivinityGainTracker.HasGainedThisTurn(Owner)` 判断，条件满足打第二段伤害。
- 斩落：用整数安全判断 `CurrentHp * 2 <= MaxHp`（严格对应“不高于 50%”），伤害前判定。
- 穿刺：普通单体伤害。

## 4. 本地化

- `Believer/localization/eng/cards.json` / `zhs/cards.json`：8 张卡的英文/中文名称与描述。
- `Believer/localization/eng/powers.json` / `zhs/powers.json`：4 个 Power 的 title/description/smartDescription
  （Power 描述用 `{Amount:diff()}`，与卡牌实际施加的层数一致）。

## 5. 新增 / 修改文件

新增：

```
BelieverCode/Cards/Basic/ArmorBreak.cs        破甲
BelieverCode/Cards/Basic/Pursuit.cs           追击
BelieverCode/Cards/Basic/Pierce.cs            穿刺
BelieverCode/Cards/Basic/SeveringStrike.cs    斩落
BelieverCode/Cards/Basic/Beacon.cs            信标
BelieverCode/Cards/Basic/Guardian.cs          守护
BelieverCode/Cards/Basic/Bestow.cs            施予
BelieverCode/Cards/Basic/Concentration.cs     专注
BelieverCode/Powers/Basic/BeaconPower.cs
BelieverCode/Powers/Basic/GuardianPower.cs
BelieverCode/Powers/Basic/BestowPower.cs
BelieverCode/Powers/Basic/ConcentrationPower.cs
BelieverCode/Mechanics/DivinityGainTracker.cs
BelieverCode/Patches/DivinityGainTrackingPatch.cs
Believer/localization/zhs/powers.json
docs/M7-BasicCards3.md
（各 .cs 均带 .uid）
```

修改：

```
Believer/localization/eng/cards.json     8 张卡英文
Believer/localization/zhs/cards.json     8 张卡中文
Believer/localization/eng/powers.json    4 个 Power 英文
BelieverCode/MainFile.cs                 启动时打印神性观测补丁自检日志
```

未改动：前 16 张白卡、M1–M6 系统、神性资源代码、卡池注册代码。

## 6. Build / Publish / 部署结果

- `dotnet build Believer.csproj`：成功，**0 warning / 0 error**。
- `dotnet publish ... -p:PublishProfile=FolderProfile`：**exit 0**。
- 部署：`Believer.dll` 110,080 B、`Believer.pdb` 47,556 B、`Believer.pck` 220,136 B（11:08）。
- 启动冒烟（zh_CN）：
  - `[Patches] Divinity gain observer: prefix=1, postfix=1.`（观测补丁已挂载）；
  - 8 张新卡占位卡图全部出现在预加载日志（`armor_break` / `pursuit` / `pierce` / `severing_strike` /
    `beacon` / `guardian` / `bestow` / `concentration`）；
  - `zhs powers.json` 已加载；无本地化格式化错误。

## 7. 游戏内测试步骤

前置：mod 模式进入「信徒」一局战斗，控制台可用。`card <ID>` 生成卡牌，`upgrade <序号>` 升级，
`divinity <show|set|add|full|empty>` 控制神性，`block` 可给自己加格挡，`power <ID> <层数> <索引>` 加状态。

1. **破甲**：`card BELIEVER-ARMOR_BREAK`；目标有格挡（先 `block` 命令或打防御）→ 8+4；目标无格挡 → 仅 8；
   升级后 10+5。
2. **追击**：
   - `divinity set 0` 后立刻打出（本回合未获得过神性）→ 仅 4；
   - `divinity add 1`（本回合获得）后打出 → 4+4；升级后 6+4；
   - 新回合开始（回合开始遗物 +1 神性）后打出 → 应触发额外伤害。
3. **穿刺**：9 伤害，升级 12。
4. **斩落**：把敌人打到 ≤50% HP 后打出 → 8+5；>50% → 仅 8；升级 11+7。
5. **信标**：打出后 `divinity add 1` → 获得 3 格挡（升级 5）；本回合再次 `divinity add 1` → 不再获得格挡；
   下一回合首次获得神性 → 再次触发。
6. **守护**：打出后在回合结束前至少保留 1 点神性 → 回合结束获得 5 格挡（升级 7）；神性为 0 的回合不触发。
7. **施予**：打出后连续获得 2 次神性 → 每次触发 3 格挡（升级 4）；第三次获得 → 不再触发；下回合重新计数。
8. **专注**：打出后本回合第一次打出技能牌（如防御/静心）→ +1 神性（升级 2）；之后再打技能牌 → 不再触发；
   下回合首次技能牌再次触发。
9. **上限**：`divinity set 7` 后触发信标/施予/专注 → 神性保持 7/7。
10. 卡池：正常战斗奖励中确认这 8 张白卡会出现；图鉴确认中文名称/描述和升级数值。

## 8. 已知限制

- 卡图为占位图、无专属动画/特效（后续阶段）。
- 神性获取追踪使用“只读观测补丁”（prefix/postfix 不改 M1 行为）；若未来 BaseLib 提供正式的“获得资源”
  钩子，应改用该钩子替换此补丁。
- 信标/施予的格挡由获得事件即时结算（`TaskHelper` + `fast` 格挡，与原版 After Image 同类），
  多人环境下与游戏内其它“钩子内即时结算”一致；如后续发现多端顺序问题，可改为事件队列。
- 中文表覆盖当前 24 张白卡；蓝卡批次尚未开始，完成后一并补齐中文。
- 本批只实现 8 张，等待确认后再开始第四批（8 张蓝卡）。

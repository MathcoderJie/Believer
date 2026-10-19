# M6 信徒的 12 张正式神迹 · 设计与实现说明

> **M10.4 更新**：12 张神迹现在都是正常稀有卡（BelieverCardPool/Rare），普通状态可打出；
> 奖励获取和全知全能强化形态见 `docs/M10.4-MiracleRareRefactor.md`。本文以下“仅形态内可用/奖励留到 M11”属于历史设计。
>
> 本阶段只实现 12 张神迹的**卡牌定义、效果与本地化**。神迹奖励获取机制留到 M11，
> 因此游戏内测试请使用开发命令 `miracle add <卡牌ID>` 把神迹加入本局收藏。
> 不制作专属动画（视觉留到 M7），不修改金卡奖励/火堆/神谕/普通卡/遗物。

## 1. 统一规则（全部 12 张一致）

- 费用 **0**（卡牌定义里的基础费用就是 0；全知全能期间 Power 再提供全局 0 费，二者一致）。
- 只能在全知全能形态内使用：继承 `MiracleCard`，`IsPlayable` 由基类检查 `OmnisciencePower`。
- 不进入普通主卡组：神迹没有 `Pool` 之外的任何牌组入口，只由 M5 在全知全能期间用
  `ICombatState.CreateCard` 生成临时实例；战斗结束即消失。
- 进入全知全能时由 M5 生成本局已收藏神迹；形态结束时由 M5 清理未使用神迹。
- 打出后自带 `Exhaust`（`MiracleCard.CanonicalKeywords`），不会洗回抽牌堆或进入永久牌组。
- 全部为 `CardRarity.Token`，位于 `BelieverSignatureCardPool`（非角色池、非共享池），
  **不会出现在卡牌奖励、商店或随机生成中**，`showInCardLibrary = false`。

## 2. 卡牌与实现

所有卡牌位于 `BelieverCode/Cards/Miracles/`，每张独立文件（不塞进巨型类）。

| # | 神迹 | 卡牌 ID | 目标 | 实现要点 |
|---|---|---|---|---|
| 1 | 要有光 | `BELIEVER-LET_THERE_BE_LIGHT` | Self（全体敌人） | `DamageCmd.Attack(30).TargetingAllOpponents` + `PowerCmd.Apply<VulnerablePower>(2)` |
| 2 | 再创世 | `BELIEVER-GENESIS` | Self | `Omniscience.RequestFormExtension` → Power 记录“延长一次” |
| 3 | 天启四骑士 | `BELIEVER-FOUR_HORSEMEN` | Self | 四段依次执行；死亡取 `HittableEnemies.OrderBy(CurrentHp).First()` |
| 4 | 红海裂天 | `BELIEVER-PARTING_OF_THE_RED_SEA` | Self | 30 全体伤害 + `CreatureCmd.GainBlock(40)` + 复用原版 `BlurPower`（保留格挡到下一回合） |
| 5 | 十灾降世 | `BELIEVER-TEN_PLAGUES` | Self | `Attack(6).WithHitCount(10).TargetingRandomOpponents`；引擎每次命中独立随机选目标 |
| 6 | 耶城崩塌 | `BELIEVER-FALL_OF_JERICHO` | Self | 先 `CreatureCmd.LoseBlock(敌人, 全部格挡)`，再 25 全体伤害 + 2 易伤 |
| 7 | 天火审判 | `BELIEVER-JUDGMENT_OF_FIRE` | AnyEnemy | 70 单体；`target.IsDead` 后对剩余敌人 25（死亡目标不再在 `HittableEnemies` 中） |
| 8 | 羔羊七印 | `BELIEVER-SEVEN_SEALS` | Self | `Attack(7).WithHitCount(7)` 全体 + 额外 15 全体 |
| 9 | 五饼二鱼 | `BELIEVER-FIVE_LOAVES_AND_TWO_FISH` | Self | 从手牌选普通牌；`CardModel.CreateClone()` ×3，`SetToFreeThisTurn()`，加入手牌；由 `LoavesAndFishesPower` 在回合末移除 |
| 10 | 拉撒复生 | `BELIEVER-LAZARUS_RISES` | Self | `CardSelectCmd.FromCombatPile(消耗堆, 0..3)` → `CardPileCmd.Add(手牌)` + `SetToFreeThisTurn`；只移动原实例，不复制 |
| 11 | 万军降临 | `BELIEVER-LORD_OF_HOSTS` | Self | 施加 `ArmyOfTheLordPower`（8 层）；Power 在 `AfterCardPlayed` 中每次普通牌触发 5 全体伤害并减 1 层 |
| 12 | 白座终审 | `BELIEVER-WHITE_THRONE_JUDGMENT` | Self | 35 全体；Boss 额外 40；所有伤害结算后，非 Boss 且当前 HP < 30% 最大 HP 的直接 `CreatureCmd.Kill` |

本地化：`Believer/localization/eng/cards.json` 增加 12 组 `.title`/`.description`
（五饼二鱼与拉撒复生另有 `.selectionScreenPrompt`），`powers.json` 增加两个 Power 的三段文本。

## 3. 关键机制与 API（均来自实际反编译源码核查，非猜测）

| 需求 | 实际 API / 机制 |
|---|---|
| 全体伤害 | `DamageCmd.Attack(decimal).FromCard(this).TargetingAllOpponents(ICombatState)` |
| 随机多段 | `Attack(...).WithHitCount(n).TargetingRandomOpponents(combatState)`；命中循环内每段独立 `Rng.CombatTargets.NextItem`，因此“每次攻击独立结算” |
| 单体伤害 | `Attack(...).Targeting(Creature)`（`CardPlay.Target`） |
| 易伤/虚弱 | `PowerCmd.Apply<VulnerablePower>/<WeakPower>(ctx, 敌人集合, 2m, Owner.Creature, this)` |
| 格挡 | `CreatureCmd.GainBlock(Owner.Creature, new BlockVar(40m, ValueProp.Move), cardPlay)` |
| 保留格挡 | 原版 `BlurPower`：`ShouldClearBlock` 阻止拥有者格挡清空，次回合 `AfterSideTurnStart` 自减 1 后消失 |
| 移除格挡 | `CreatureCmd.LoseBlock(enemy, enemy.Block)`（原版卡 414781 行同款） |
| 复制手牌 | `CardModel.CreateClone()`（引擎克隆，带 `CloneOf` 标记，仅战斗内）+ `CardPileCmd.AddGeneratedCardToCombat(..., PileType.Hand, owner)` |
| 费用变 0（本回合） | `CardModel.SetToFreeThisTurn()`（只加回合内修正，不改永久费用） |
| 从消耗堆取牌 | `CardSelectCmd.FromCombatPile(ctx, PileType.Exhaust.GetPile(player), player, prefs, filter)` → `CardPileCmd.Add(card, PileType.Hand)` |
| 每次出牌触发 | `PowerModel.AfterCardPlayed(ctx, CardPlay)`；每次出牌结算**一次**（多段伤害/卡内效果不会重复触发）；`CardPlay.IsFirstInSeries` 避免回响牌重复计数 |
| 直接击杀 | `CreatureCmd.Kill(Creature)`（原版处决类效果同款） |
| Boss 判定 | 本机反编译确认 `MonsterModel` 没有 Boss 标记，因此用遭遇房间类型：`creature.CombatState.Encounter.RoomType == RoomType.Boss` |
| 手牌空间 | 复用 M5 的 BaseLib `IMaxHandSizeModifier`；M6 增加 `OmnisciencePower.ReserveHandSpace`，让五饼二鱼的复制品/拉撒复生回收的牌在满手时也真正进手牌（不会触发引擎“手牌满→弃牌堆”回退） |

## 4. 再创世（重点）

实现分布在三处：

1. `Genesis.OnPlay` → `Omniscience.RequestFormExtension(Owner)`；
2. `OmnisciencePower.RequestExtension()`：`_extensionRequested` / `ExtensionGranted` 双标记，
   第二次请求直接返回 false（**每次全知全能最多延长一次，无法通过重复触发自我无限延长**；
   且再创世本身打出后 Exhaust，M5 也不会重复生成）；
3. 生命周期：
   - 玩家回合结束（`BeforeSideTurnEnd`）：若已请求延长，**不清理**手牌中的神迹，而是对未打出的神迹
     `GiveSingleTurnRetain()`，让它们保留到延长回合继续可用（不重新生成、不重新抽启动的 3 张、
     不重新清 Debuff）；
   - 敌方回合结束（`AfterSideTurnEnd`）：第一次请求 → 授予延长，**不移除 Power**（免伤、0 费、
     神迹可用性、手牌上限全部继续生效）；之后的敌方回合结束 → 正常移除 Power 并归还全知全能卡；
   - 延长回合的玩家回合结束时恢复正常清理（未使用神迹移除），随后形态正常结束。

## 5. 新增 / 修改文件

新增（卡牌 12 + 规则 1）：

```
BelieverCode/Cards/Miracles/MiracleRules.cs            普通牌/临时复制品/神迹/Boss 判定
BelieverCode/Cards/Miracles/LetThereBeLight.cs
BelieverCode/Cards/Miracles/Genesis.cs
BelieverCode/Cards/Miracles/FourHorsemen.cs
BelieverCode/Cards/Miracles/PartingOfTheRedSea.cs
BelieverCode/Cards/Miracles/TenPlagues.cs
BelieverCode/Cards/Miracles/FallOfJericho.cs
BelieverCode/Cards/Miracles/JudgmentOfFire.cs
BelieverCode/Cards/Miracles/SevenSeals.cs
BelieverCode/Cards/Miracles/FiveLoavesAndTwoFish.cs
BelieverCode/Cards/Miracles/LazarusRises.cs
BelieverCode/Cards/Miracles/LordOfHosts.cs
BelieverCode/Cards/Miracles/WhiteThroneJudgment.cs
BelieverCode/Powers/Miracles/ArmyOfTheLordPower.cs    万军降临的触发计数（8 层，回合末消失）
BelieverCode/Powers/Miracles/LoavesAndFishesPower.cs  五饼二鱼复制品的回合末清理（隐藏 Power）
docs/M6-TwelveMiracles.md
```

修改：

```
BelieverCode/Powers/OmnisciencePower.cs    再创世延长逻辑 + 神迹保留 + 额外手牌预留
BelieverCode/Mechanics/Omniscience.cs      RequestFormExtension / ReserveHandSpace
Believer/localization/eng/cards.json        12 张神迹的 title/description/选择提示
Believer/localization/eng/powers.json       2 个新 Power 的 title/description/smartDescription
```

未改动：M1 神性、M2 启动/结束主流程与形态效果、M3 遗物、M4 收藏与存档、M5 生成/清理主流程、
神迹奖励获取（M11）、普通卡/遗物/火堆/神谕。

## 6. 验证记录

- `dotnet build Believer.csproj`：成功，**0 warning / 0 error**（含 ModAnalyzers 的本地化/池子检查）。
- `dotnet publish Believer.csproj -p:PublishProfile=FolderProfile`：exit 0；
  部署 `Believer.dll` / `Believer.pdb` / `Believer.pck` 均更新，dll 中已确认包含 12 张卡与 2 个 Power。
- 启动冒烟：mod 初始化成功，`Believer-miracle-collection` 存档注册成功，两个调试命令注册成功，
  无异常、无 Harmony 失败、无本地化缺失报错。
- 具体战斗内效果需按第 7 节逐张验证（本阶段未做自动化战斗测试）。

## 7. 游戏内逐张测试清单

前置：mod 模式进入「信徒」的一局战斗，打开开发者控制台；先 `divinity full`，
需要测试全部 12 张时用 `miracle add <ID>` 逐张加入收藏（或分批加入），再打出全知全能。

> 收藏命令示例：`miracle add BELIEVER-LET_THERE_BE_LIGHT`（ID 见第 2 节表格）。
> 每次改动收藏后需要重新进入一次全知全能才会生成新手牌。

| 神迹 | 验证点 |
|---|---|
| 要有光 | 全体 30 伤害；每个敌人 +2 易伤；多敌人时全部命中 |
| 再创世 | 打出后本回合结束形态仍保留；延长回合内免伤/0 费/未打出神迹仍可用；延长回合结束形态正常结束；再次打出（若能获得第二张）应无效；不能无限延长 |
| 天启四骑士 | 顺序：15+2易伤 → 20 → 15+2虚弱 → 最低 HP 敌人 45；每段独立结算 |
| 红海裂天 | 全体 30；获得 40 格挡；下一玩家回合开始时格挡未被清空，随后正常消失 |
| 十灾降世 | 10 段 ×6，随机目标可重复；每段独立受易伤等修正影响 |
| 耶城崩塌 | 有格挡敌人格挡先清零；25 全体伤害；+2 易伤 |
| 天火审判 | 单体 70；击杀目标后其他敌人各 25；未击杀时无额外伤害 |
| 羔羊七印 | 7×7 全体 + 15 全体，共 8 段；敌人中途死亡后剩余段不再打该目标 |
| 五饼二鱼 | 选择手牌普通牌；手牌出现 3 张同名复制品；费用 0；回合结束全部消失（手牌/抽牌堆/弃牌堆/消耗堆均无残留）；不能选神迹或复制品 |
| 拉撒复生 | 消耗堆选择 0~3 张普通牌；进入手牌且本回合费用 0；不能选神迹/复制品；回收后原卡不会重复出现；永久费用不变 |
| 万军降临 | 打出后每张普通牌触发一次全体 5 伤害；最多 8 次；神迹牌不触发；多段伤害卡只触发 1 次；回合结束 Power 消失 |
| 白座终审 | 全体 35；Boss 额外 40（Boss 房间内敌人按 Boss 处理）；非 Boss 且 HP<30% 的在伤害后直接死亡；击杀判定发生在伤害之后 |

## 8. 已知限制与建议

1. **Boss 判定**：本机 STS2 反编译中 `MonsterModel`/`EncounterModel` 没有“单只怪物是 Boss”的标记，
   因此按“Boss 遭遇房间内的敌人都算 Boss”实现（Boss 房中的小怪也会吃 40 额外伤害、不吃处决）。
   如果后续需要真正区分 Boss 与小怪，建议 M7 之后用遭遇内主 Boss 的怪物 ID 列表来精确定义。
2. **万军降临与回响牌**：`AfterCardPlayed` 对回响（同一张牌连续打出多次）会多次回调，本实现只统计
   `IsFirstInSeries`，即“一张牌的一次使用”算一次触发，避免一次回响多次计数。
3. **五饼二鱼/拉撒复生与满手**：通过 M5 手牌上限扩展 + M6 额外预留保证卡牌进入手牌；极端情况下
   （其他效果同时塞牌）仍会记录 WARN/ERROR 日志，不会静默丢失。
4. **拉撒复生从消耗堆取牌**：本机原版卡没有同类“从消耗堆回手”的实现可对照，走的是通用
   `CardPileCmd.Add(手牌)` 路径，需要在游戏内确认视觉/结算正常（已列在第 7 节测试项）。
5. 本阶段**没有**神迹专属动画/特效，也没有奖励获取途径（M11）；测试依赖开发控制台。

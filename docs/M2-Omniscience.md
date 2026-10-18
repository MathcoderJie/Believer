# M2 全知全能 · 设计与实现说明

> **M5 更新**：文中提到的开发占位 `DevMiracleProvider` 已被 `MiracleCollectionProvider` 取代
> （全知全能现在只发放本局收藏的神迹），神迹生成/手牌上限/清理逻辑见 `docs/M5-OmniscienceMiracles.md`。
> 本文其余 M2 设计仍然有效。

本文件记录里程碑 M2「全知全能核心机制」的设计决策、实现方式、接口约定与验证结果。
M1 的神性（Divinity）系统保持原样，仅做增量接入。

---

## 1. 启动入口决策

需求：神性满 7 点时，玩家主动启动；启动消耗 7 神性；形态持续到回合结束。

检查游戏真实可用的接口后（反编译 `sts2.dll` / BaseLib 3.4.7 + BaseLib Wiki），结论：

| 方案 | 可行性 | 结论 |
|---|---|---|
| 战斗 HUD 自绘按钮（BaseLib `ExtraCombatUi` + `NClickableControl`） | 可实现 | **未采用**：点击后必须通过 `RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue` 派发自定义 `GameAction`，而自定义动作还需要配套 `INetAction` 结构体才能联机同步。BaseLib 不提供任何 `GameAction`/`ChoiceContext` 辅助 API，自己维护这条链路成本与风险都最高。 |
| 自定义 `GameAction` + `INetAction` 按钮 | 游戏会自动注册 mod 的 `INetAction` 子类型，理论可行 | **未采用**：改动面大、需要处理暂停/队列/回放/校验和，M2 不值得为此承担联机风险。 |
| **把「全知全能」做成一张真实卡牌** | 完全走原生卡牌流程 | **采用**：卡牌打出即原生 `PlayCardAction`，自动获得玩家选择上下文、动画、队列顺序、联机同步与校验和。这是兼容性最高的「主动启动」入口。 |
| 药水 / 遗物 / Power 点击 | 药水要占格且消耗品；Power 只支持悬浮提示不可点击；遗物无激活语义 | 不采用 |

最终形态：

- 新增不可收藏的签名卡 `OmniscienceCard`（全知全能，0 费技能牌，目标自身）。
- 卡牌关键字 `Innate`（战斗起始手牌必在手）+ `Retain`（回合结束不弃，常驻手牌）+ `Eternal`（永恒：在牌组中无法被移除/转化）。
- **无法被丢弃**：引擎没有「不可丢弃」关键字或 hook（`Retain` 只挡回合结束弃手牌），因此由
  `Patches/OmniscienceCardPatches.cs` 在全部丢弃入口上拦截：
  - `CardCmd.DiscardAndDraw`（`CardCmd.Discard` 两个重载最终都走它）→ 把启动卡从待弃列表中剔除，
    卡不会离开手牌，也不会触发「被丢弃」相关的 hook/历史；
  - `CardSelectCmd.FromHandForDiscard` → 包一层过滤器，让「选择若干张牌丢弃」的 UI 永远不提供这张卡
    （`CardSelectCmd.FromHand` 对空候选列表是安全的，会直接返回空）。
- `IsPlayable` 仅在「神性 7/7 且当前未处于形态」时为 true，否则卡面置灰（`UnplayableReason.BlockedByCardLogic`）；可用时金色高亮（`ShouldGlowGoldInternal`）。
- 打出后卡牌不会消耗：`GetResultPileTypeForCardPlay() => PileType.Hand` 使其直接回到手牌；若手牌已满被挤入弃牌堆，形态结束时由 `Omniscience.RestoreActivationCard` 找回。
- 卡牌进入角色起始卡组（`Believer.StartingDeck`），因此每次战斗必定拿到。它属于「签名卡」，不是可收藏卡：放在独立的非角色、非共享卡池 `BelieverSignatureCardPool` 中，不会进奖励/商店/随机生成/卡牌图鉴。

> 说明：`CardModel.Pool` 默认会在 `ModelDb.AllCardPools`（角色池 + 共享池）中查找，找不到会抛
> `Card ... is not in any card pool!`。签名卡池两者都不是，因此 `BelieverSignatureCard.Pool`
> 直接重写为返回签名池实例；这样既解决视觉解析，又保持卡池不可见。

---

## 2. 需求 → 实现对照

| 需求 | 实现 |
|---|---|
| 神性达到 7 点允许启动 | `OmniscienceCard.IsPlayable` 检查 `BelieverDivinityResource.IsFull`；打出入口是原生卡牌 |
| 启动卡无法被丢弃（恒定在手） | `Retain` 挡回合结束弃手牌；`Patches/OmniscienceCardPatches.cs` 拦截 `CardCmd.DiscardAndDraw`（所有丢弃效果的唯一执行入口）并把它从 `CardSelectCmd.FromHandForDiscard` 的候选中排除 |
| 启动卡「永恒」特性 | `CardKeyword.Eternal`：`CardModel.IsRemovable`/`IsTransformable` 在牌组中为 false，无法被移除/转化 |
| 启动消耗 7 点神性 | `Omniscience.Activate` → `BelieverDivinityResource.TrySpend(MaxAmount)`（在卡牌结算内执行，联机每个客户端都会执行同一序列） |
| 持续当前回合（按 M2 确认：覆盖敌方回合） | `OmnisciencePower` 在启动时施加，在敌方回合结束的 `AfterSideTurnEnd` 移除 |
| 立即抽 3 张牌 | `CardPileCmd.Draw(choiceContext, 3, player)` |
| 清除所有可清除的 Debuff | 枚举 `Creature.Powers` 中 `TypeForCurrentAmount == PowerType.Debuff` 的能力，逐个 `PowerCmd.Remove`（游戏没有现成「清 Debuff」API，这是游戏自身（Misery/Rend）判断 Debuff 的惯用法；`PowerCmd.Remove` 是唯一会触发 `AfterRemoved` 的移除路径） |
| 手牌费用变 0，回合结束恢复原费用 | `OmnisciencePower.TryModifyEnergyCostInCombat` 全局返回 0（仅本玩家卡牌）。这是动态修正，后抽到的牌也免费；**从不写 `CardEnergyCost.Canonical/_base`**，能力移除后费用自然恢复 |
| 获得本局已解锁的全部神迹牌 | `MiracleRegistry.GetUnlockedMiracles` → `ICombatState.CreateCard` 建实例 → `CardPileCmd.AddGeneratedCardsToCombat(..., PileType.Hand, ...)` |
| 神迹仅在全知全能期间使用 | `MiracleCard.IsPlayable` 要求拥有 `OmnisciencePower`；`MiracleCard` 自带 `Exhaust`，打出即离开战斗；未使用的在玩家回合结束时统一移除 |
| 本回合不损失生命值（含多段攻击） | `OmnisciencePower.ModifyHpLostAfterOsty` + `...Late` 对 owner 返回 0。`Hook.ModifyHpLost` 是所有攻击/自伤/中毒失血的唯一入口；多段攻击每段独立结算，因此每段都被清零。格挡仍正常吸收、正常触发格挡破碎与受击事件（与游戏原生 Buffer/Intangible 一致；被完全减免的那一下不会显示伤害数字，这是引擎行为） |
| 回合结束退出形态、清理未使用临时神迹、恢复临时效果 | 玩家回合结束：`BeforeSideTurnEnd`（在弃手牌之前）移除所有残留 `MiracleCard`；敌方回合结束：`AfterSideTurnEnd` 移除 `OmnisciencePower`（费用/免伤随之失效）并把启动卡放回手牌 |
| 不通过修改最大生命值实现免伤 | 使用 HP-loss hook，不改 `MaxHp` |
| 不永久修改卡牌原始费用 | 只用动态全局费用修正，卡牌 canonical 费用不变 |
| 不重写 M1、不引入信仰系统 | M1 资源/UI/测试卡未改；只做了 `StartingDeck` 追加、`MainFile.Initialize` 注册、本地化追加 |

---

## 3. 新增 / 修改文件

新增：

```
BelieverCode/Cards/Signature/BelieverSignatureCardPool.cs   非收藏卡牌池（非角色池/非共享池）
BelieverCode/Cards/Signature/BelieverSignatureCard.cs       签名卡基类（Pool 重写、立绘回退、不可升级）
BelieverCode/Cards/OmniscienceCard.cs                       全知全能启动卡（Innate+Retain+Eternal，7/7 可打，回手，不可丢弃）
BelieverCode/Patches/OmniscienceCardPatches.cs              不可丢弃：拦截所有丢弃命令与「选择要丢弃的牌」候选
BelieverCode/Powers/OmnisciencePower.cs                     全知全能形态：费用 0、免伤、回合结束清理/退出
BelieverCode/Mechanics/Omniscience.cs                       启动序列（消耗 7 → 施加形态 → 清 Debuff → 抽 3 → 发神迹）及清理/回手辅助
BelieverCode/Miracles/IMiracleProvider.cs                   神迹解锁来源接口（收藏系统 M3 接入点）
BelieverCode/Miracles/MiracleRegistry.cs                    神迹注册表（去重、稳定顺序、错误隔离）
BelieverCode/Miracles/MiracleCard.cs                        神迹卡基类（形态内可打、Exhaust、回合结束清理约定）
BelieverCode/Miracles/DevMiracleProvider.cs                 【开发占位】神迹提供者
BelieverCode/Cards/Dev/DevMiracleTestCard.cs                【开发占位】1 张测试神迹（非 12 张正式神迹）
```

修改：

```
BelieverCode/Character/Believer.cs          StartingDeck 追加 OmniscienceCard
BelieverCode/MainFile.cs                    Initialize 中注册 DevMiracleProvider
Believer/localization/eng/cards.json        新增启动卡、测试神迹的标题/描述
Believer/localization/eng/powers.json       新增 OmnisciencePower 的 title/description/smartDescription
```

所有新增 `.cs` 均配套 `.uid`（Godot 工程约定）。

---

## 4. 神迹（Miracle）接口设计

神迹收藏系统尚未开发，M2 只交付「必要接口 + 1 张开发测试牌」：

```csharp
// 收藏系统实现它，报告“本局已解锁”的 canonical 神迹卡模型
public interface IMiracleProvider
{
    IEnumerable<CardModel> GetUnlockedMiracles(Player player);
}

// 接口位于 Player；实现必须是确定性的（联机各端结果一致，否则校验和会分叉）
MiracleRegistry.Register(provider);
MiracleRegistry.GetUnlockedMiracles(player); // 去重（按 ModelId）、保持注册顺序、单个 provider 抛异常不影响其它
```

具体神迹卡：继承 `MiracleCard`，自动获得「仅形态内可打 + Exhaust + 回合结束清理」契约，以及必须的
`[Pool(typeof(BelieverSignatureCardPool))]`。

M3（收藏系统）接入方式：实现 `IMiracleProvider`（从存档/解锁状态读取本局解锁的神迹）并在初始化时
`MiracleRegistry.Register(...)`，同时新增 12 张 `MiracleCard` 子类；删除 `DevMiracleProvider` 与
`DevMiracleTestCard` 即可，全知全能核心代码无需改动。

---

## 5. 已知限制 / TODO

1. 正式 12 张神迹与收藏系统未实现（M2 范围外，只有 1 张 dev 测试神迹）。
2. 神迹一次全部塞进手牌；手牌上限为 10，超出的神迹会被引擎转入弃牌堆（本回合仍可抽到打出，回合结束统一清理）。未来神迹数量多时需要专门的展示/选择界面。
3. 启动卡是起始卡组里的真实卡牌：旧存档中已有的牌组不会自动获得它（新开一局生效）。卡牌自带
   `Eternal`，因此无法被移除/转化（M2 之前的存档/通过控制台强删属例外）；后续若要彻底不依赖牌组，
   可改为 HUD 按钮 + 自定义 `INetAction`。
4. 「不可丢弃」覆盖所有走 `CardCmd.Discard*` 的丢弃效果与丢弃选择 UI。手牌满导致启动卡被引擎从
   Play 堆回退到弃牌堆这类内部堆栈整理不属于「丢弃」，仍可能发生，形态结束时由
   `Omniscience.RestoreActivationCard` 找回。
5. 启动卡/形态/测试神迹暂无专用立绘，走 mod 占位图（启动日志中的 “Could not find … omniscience_*.png” 属预期回退）。
6. 联机行为按原生卡牌/能力流程实现，未做多人实机联调。
7. 被动数值（格挡、事件）仍会正常触发；被完全免掉的攻击不显示伤害数字，与原生 Intangible/Buffer 一致。

---

## 6. 验证记录

- `dotnet build Believer.csproj`：成功，0 warning / 0 error。
- `dotnet publish Believer.csproj -p:PublishProfile=FolderProfile`：成功（exit 0），生成并部署
  `Believer.dll` / `Believer.pdb` / `Believer.pck` 到 `<游戏目录>/mods/Believer/`。
- 启动游戏冒烟测试（经 Steam 启动后关闭）：mod 初始化成功（`harmony.PatchAll` 应用了
  `OmniscienceCardPatches` 且无 Harmony 异常）、`BelieverDivinityResource` 注册成功、
  `ModelDb.Preload` 正常解析新卡牌 Pool/立绘/能力图标，日志无异常（仅占位图回退信息）。

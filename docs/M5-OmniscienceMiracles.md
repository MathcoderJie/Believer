# M5 全知全能 × 临时神迹系统 · 设计与实现说明

> 本阶段不实现 12 张正式神迹、不做神迹专属动画、不改金卡奖励/火堆祈祷/神谕/其他普通卡与遗物。

## 1. 目标

进入全知全能形态时，把**本局已收藏（M4）**的神迹生成为本次形态专用的临时卡牌，费用为 0，
形态内可用，形态结束后全部清理，且不进入永久卡组、不影响 M4 收藏。

## 2. 开发前实际接口核查结论

| 检查项 | 实际情况（以本机 sts2.dll / BaseLib 3.4.7 反编译源码为准） |
|---|---|
| M2 启动/结束 | 启动：`OmniscienceCard.OnPlay` → `Mechanics.Omniscience.Activate`（扣 7 神性 → 施加 `OmnisciencePower` → 清 Debuff → 抽 3 → 发神迹）。结束：`OmnisciencePower.AfterSideTurnEnd`（敌方回合结束）移除 Power，费用/免伤随 Power 失效而恢复。**M5 沿用，未重复扣除神性**。 |
| M4 收藏接口 | `MiracleCollectionStore.GetOwnedIds / GetOwnedMiracleCards`、`TryAdd/Has/Clear`；`MiracleCatalog` 自动发现所有具体 `MiracleCard`；未解析 ID 会被跳过并告警。 |
| 临时卡牌生成 | `ICombatState.CreateCard(canonical, owner)` 生成战斗内可变实例（与牌组无关），`CardPileCmd.AddGeneratedCardsToCombat(cards, PileType.Hand, player)` 放入手牌并记录“combat 生成”。 |
| 移除 | `CardPileCmd.RemoveFromCombat(card)`（从当前战斗牌堆移除并标记，不进牌组）。 |
| 手牌上限 | 引擎常量 `CardPile.MaxCardsInHand = 10`；**手牌满时 `CardPileCmd.Add` 会把卡静默改放到弃牌堆**（sts2 468135 行）。直接生成 12 张神迹会溢出丢失。 |
| 可用的兼容机制 | BaseLib `MaxHandSizePatch` 把 `CardPileCmd.Add`/`CardPileCmd.Draw`/抽牌检查等处的 10 全部改为 `GetMaxHandSize(player, 10)`，并公开扩展点 `BaseLib.Hooks.IMaxHandSizeModifier`（任意 hook 监听者，包括 Power）。本机日志无 “Failed to find player reference in CardPileCmd.Add” 警告，说明该补丁已生效。 |
| 费用 | 形态内 `OmnisciencePower.TryModifyEnergyCostInCombat` 动态把该玩家所有卡（含生成的神迹）费用变 0；**不写 canonical cost**，形态结束即恢复。 |
| 播放/联机 | 全部逻辑位于原生卡牌结算（`PlayCardAction`）与原生 Power 生命周期内，多端确定性一致。 |

## 3. 实现

### 3.1 M4 → M2 的桥接（provider）

新增 `MiracleCollectionProvider : IMiracleProvider`（替换 M2 的 `DevMiracleProvider`，后者会无视收藏白送测试神迹，违反 M5"未获得的神迹不能生成"）：
Omniscience 通过 M2 的 `MiracleRegistry` 查询，provider 返回**本局收藏**解析出的 canonical 神迹卡模型。
`MiracleRegistry` 原本就按 `ModelId` 去重并跳过非 `MiracleCard` 项，未知/失效 ID 由 M4 收藏层安全跳过。

### 3.2 生成流程（`Mechanics.Omniscience`）

`Activate` 在 M2 顺序上增加了 M5 步骤：

1. 扣 7 神性 → 施加 `OmnisciencePower`（M2，不变）
2. 清可清除 Debuff（M2，不变）
3. **查询并去重本局已收藏神迹**（`CollectCollectedMiracles`：过滤 `MiracleCard` + 按 `ModelId` 去重）
4. **`power.SetMiraclesToGrant(count)`**：为形态预留 `count` 点额外手牌上限（见 3.3）
5. 抽 3 张（M2，不变；此时上限已提高，不会被手牌满吞掉）
6. **`GrantMiracles`**：
   - `power.MiraclesGranted` 保证**一次形态只生成一次**（重复事件不会重复生成）
   - 先 `CleanUpMiracles` 清掉任何残留，再 `CreateCard` + `AddGeneratedCardsToCombat(手牌)`
   - `EnsureMiraclesInHand` 校验每张卡确实落在手牌；若引擎仍把某张放进弃牌堆，会尝试移回并**大声记录 ERROR**，绝不静默丢弃
   - 日志：`[Miracles] Generated N miracle(s) for Omniscience; hand is now X card(s).`

### 3.3 手牌上限方案（要求 4）

- 采用 BaseLib 官方扩展点：`OmnisciencePower : IMaxHandSizeModifier`，
  `ModifyMaxHandSize(player, current) => player == 本形态拥有者 ? current + 本形态神迹数 : current`。
- 因 BaseLib 已把 `CardPileCmd.Add` 的手牌满判断、抽牌上限、起始抽牌上限都改为调用该接口，
  生成神迹时不会再被“手牌满→改放弃牌堆”的规则吞掉，**所有已获得神迹都在手中可用**。
- 上限提高只持续形态存续期间（Power 移除即恢复）。没有绕过引擎、没有写死突破限制、没有静默丢弃；
  若补丁在某个游戏版本失效，`EnsureMiraclesInHand` 会输出错误日志而不是悄悄丢牌。

### 3.4 清理（要求 3）

`Omniscience.CleanUpMiracles(player)` 扫描 `PlayerCombatState.AllCards`（手牌 / 抽牌堆 / 弃牌堆 / 消耗牌堆 / Play 区）：

- 按 `MiracleRegistry.IsMiracle`（即 `card is MiracleCard`）识别临时神迹，逐个 `CardPileCmd.RemoveFromCombat`；
- 已打出（Exhaust 堆）的神迹同样移除，**不会进入永久牌组**；
- 正在结算（Play 区）的卡不强行移除，交由卡牌流程收尾，并记录；
- 清理后再次统计残留，有残留则记录 ERROR；
- 只删卡牌实例，**不触碰 M4 收藏数据**，也不碰任何普通卡。

触发点（两处，覆盖正常与异常结束）：
1. `OmnisciencePower.BeforeSideTurnEnd`（玩家回合结束、弃手牌之前）——M2 原位置，保留；
2. `OmnisciencePower.AfterRemoved`（形态被移除时，包括敌方回合结束的正常退出，以及被其他效果提前移除）——M5 新增的最终清扫。

### 3.5 与普通卡区分、不写永久卡组

- 临时神迹只由 `MiracleCard` 子类构成（独立基类，非 `BelieverCard` 普通卡），由 `ICombatState.CreateCard` 创建、只存在于战斗牌堆；
- 从不调用任何加入 `Deck` 的接口；战斗结束生成卡自然消失；
- 费用只在形态内由 Power 动态置 0，`CardEnergyCost.Canonical` 从未被修改。

## 4. 新增 / 修改文件

新增：

```
BelieverCode/Miracles/MiracleCollectionProvider.cs   M4 收藏 → M2 IMiracleProvider 桥接
BelieverCode/Dev/DivinityConsoleCmd.cs               【开发调试】divinity 命令（M5 测试用）
docs/M5-OmniscienceMiracles.md                       本说明
```

修改：

```
BelieverCode/Mechanics/Omniscience.cs                接入 M4 收藏、预留手牌上限、去重/校验、清理改名并加最终清扫
BelieverCode/Powers/OmnisciencePower.cs              实现 IMaxHandSizeModifier；AfterRemoved 清扫；清理调用改名
BelieverCode/Miracles/IMiracleProvider.cs            文档更新（M5 provider）
BelieverCode/Cards/Dev/DevMiracleTestCard.cs         文档更新（M5 复用为测试神迹）
BelieverCode/MainFile.cs                             注册 MiracleCollectionProvider（替换 DevMiracleProvider）
```

删除：

```
BelieverCode/Miracles/DevMiracleProvider.cs（+ .uid）  M2 开发占位 provider，被 M4/M5 收藏机制取代
```

未改动：M1 神性、M2 启动/结束主流程与形态效果、M3 遗物、M4 收藏数据结构与存档、12 张正式神迹、金卡奖励等。

## 5. M6 正式神迹注册机制（预留）

- 实现正式神迹 = 新增一个 `MiracleCard` 子类（加 `[Pool(typeof(BelieverSignatureCardPool))]`、写效果），
  `MiracleCatalog` 会**自动发现**它，无需改注册表；
- 获得神迹时调用 `MiracleCollectionStore.TryAdd(player, MiracleId.FromCard(card))` 写入本局收藏（M4 存档自动持久化）；
- 全知全能无需任何修改：M5 已经按“本局收藏”生成；
- 若还需要其他解锁来源（非收藏），实现并注册新的 `IMiracleProvider` 即可（`MiracleRegistry` 自动合并去重）；
- 测试神迹 `DevMiracleTestCard` 可在 M6 完成后删除。

## 6. 游戏内测试步骤

前置：以 mod 模式启动游戏，进入「信徒」战斗，打开开发者控制台（modded 下调试命令可用）。

1. **收藏测试神迹**：`miracle add BELIEVER-DEV_MIRACLE_TEST_CARD` → `Added miracle ...`
   （可 `miracle list` 确认。）
2. **补满神性**：`divinity full` → 神性 7/7（HUD 计数器更新）。
3. **启动全知全能**：打出「全知全能」卡（7/7 时应金色高亮且可打）。
   预期：神性清零；抽 3；Debuff 清空；**手牌中多出「Test Miracle」**；费用显示 0；
   日志 `[Miracles] Generated 1 miracle(s) ...`。
4. **使用临时神迹**：打出「Test Miracle」→ 造成 6 伤害并消耗（Exhaust）。
5. **形态结束清理**：结束回合（敌方回合结束）后：
   - 手牌、抽牌堆、弃牌堆、消耗牌堆中不再有 Test Miracle；
   - 费用恢复正常（普通卡显示原费用）；
   - 手牌上限恢复正常；
   - `miracle list` 仍显示收藏存在（M4 收藏不受影响）。
6. **重复进入**：再次 `divinity full` 并启动全知全能 → 神迹按收藏重新生成一次（不会重复叠加）。
7. **未获得不生成**：`miracle clear` 后启动全知全能 → 不生成任何神迹（日志无 Generated 行）。
8. **牌组隔离**：检查牌组/结算界面，Test Miracle 从未出现在永久牌组中。
9. 联机（可选）：以上操作走原生卡牌/命令同步，各端结果一致。

## 7. 验证记录

- `dotnet build Believer.csproj`：成功，0 warning / 0 error。
- `dotnet publish Believer.csproj -p:PublishProfile=FolderProfile`：exit 0，dll/pdb/pck 部署成功。
- 启动冒烟：mod 初始化成功；两个调试命令注册日志出现；`Believer-miracle-collection` run-save 注册成功；
  无异常/无 Harmony 失败（详见提交报告）。手牌上限与形态内生成/清理需按第 6 节在游戏内实机验证。

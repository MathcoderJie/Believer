# BUG-M10.3：稀有卡奖励无法获得神迹

> **已被 M10.4 取代**：神迹现在是正常稀有卡，普通稀有奖励直接抽取；旧的特判生成/不入牌组逻辑已移除。M10.4 见 `docs/M10.4-MiracleRareRefactor.md`。

## 1. 根因（有代码证据）

不是单点条件写错，而是**整条“稀有卡奖励 → 神迹”链路此前没有实现**：

1. **没有奖励替换代码**
   - 全项目没有任何 `CardReward` / 稀有度 / 奖励候选替换到神迹的补丁；`M6-TwelveMiracles.md` 明确写着神迹奖励获取“留到 M11”，当前项目里没有对应实现。
   - 所以普通卡牌奖励始终走原版流程，不会自动出现神迹。

2. **信徒卡池没有 Rare 卡**
   - 40 张普通信徒卡只有 `Common` / `Uncommon`，没有 `CardRarity.Rare`。
   - Boss 奖励使用 `CardRarityOddsType.BossEncounter`（原版设计为“只出 Rare”）。池子里找不到 Rare 时，`CardFactory` 的 `GetNextAllowedRarity` 会回退到 Uncommon。
   - 因此连“显示一张稀有卡再替换”这一步都不会发生。

3. **即使把神迹塞进奖励候选，原版选择路径也会把它加入牌组**
   - `CardReward.OnSelect` 对选中的卡调用 `CardPileCmd.Add(card, PileType.Deck)`。
   - 原版没有“神迹选择后登记收藏、但不入牌组”的分支；如果只替换候选，会把神迹错误加入主卡组。

4. **收藏与全知全能本身没有问题**
   - `MiracleCollectionStore` / `MiracleCatalog` / `MiracleCollectionProvider` 已经能保存、去重、读取本局神迹；问题在获取入口没有接上。

**结论：根因是奖励获取功能缺失，同时信徒卡池没有 Rare 卡导致 Boss 稀有奖励被降级；不是全知全能读取错误。**

## 2. 修复

### 2.1 稀有奖励候选替换

新增 `BelieverCode/Patches/MiracleRewardAcquisition.cs`：

- Harmony postfix：`CardFactory.CreateForReward(Player, int, CardCreationOptions)`。
- 仅对 Believer 生效。
- 规则：
  - `CardCreationSource.Encounter` + `CardRarityOddsType.BossEncounter`（Boss 稀有奖励）：将 3 个候选都替换为未拥有的正式神迹。
  - 其他奖励中实际出现 `CardRarity.Rare` 的候选：逐个替换为神迹。
  - 普通 Common / Uncommon 奖励不做替换。
- 神迹来源：`MiracleCatalog.KnownIds`，排除：
  - 已拥有神迹；
  - 开发测试神迹 `DevMiracleTestCard`。
- 没有可用神迹时：保留原卡奖励，不产生空选项，不崩溃。
- 不修改全局稀有度逻辑，不影响其他角色。

### 2.2 选择后登记，而不是加入主卡组

同一文件新增 Harmony prefix：`CardPileCmd.Add(CardModel, PileType, ...)`。

仅在以下条件拦截：

- 目标 pile 是 `PileType.Deck`；
- 卡是 `MiracleCard`。

行为：

- 调用 `MiracleCollectionStore.TryAdd(player, MiracleId.FromCard(miracle))` 登记本局收藏；
- 返回 `CardPileAddResult.success = true`，让奖励界面正常结束；
- **不执行原版入牌组逻辑**；
- 重复选择返回 `AlreadyOwned`，不会重复登记，也不会加入牌组。

## 3. 验证

### 3.1 测试方式

由于无法从主菜单自动进入真实 Boss 奖励界面，使用 dev-only 测试：

- 标记：`user://believer_reward_test.flag`
- 文件：`BelieverCode/Dev/BelieverRewardPipelineTest.cs`
- 创建一个**隔离的测试 RunState**（不读写玩家真实存档），使用真实：
  - `CardFactory.CreateForReward`
  - `CardReward` 使用的 `PileType.Deck` 添加路径
  - `MiracleCollectionStore`
  - `MiracleCollectionProvider`

### 3.2 实际日志

```text
[MiracleRewardTest] test run created; character=CHARACTER.BELIEVER-BELIEVER, deck=10
[MiracleRewards] Replaced 3 rare reward slot(s) with Miracles:
  BELIEVER-FALL_OF_JERICHO, BELIEVER-FIVE_LOAVES_AND_TWO_FISH, BELIEVER-FOUR_HORSEMEN
[MiracleRewardTest] BOSS candidates:
  BELIEVER-FALL_OF_JERICHO[Token], BELIEVER-FIVE_LOAVES_AND_TWO_FISH[Token], BELIEVER-FOUR_HORSEMEN[Token]
[MiracleRewardTest] BOSS miracle candidates=3/3
[MiracleRewards] Reward selected 'BELIEVER-FALL_OF_JERICHO' -> collection result=Added, owned=1
[MiracleRewardTest] select result=True deck 10->10 owned=True selected=BELIEVER-FALL_OF_JERICHO
[MiracleRewards] Reward selected 'BELIEVER-FALL_OF_JERICHO' -> collection result=AlreadyOwned, owned=1
[MiracleRewardTest] duplicate selection ownedCount=1 deck=10
[MiracleRewards] Replaced 3 rare reward slot(s) with Miracles:
  BELIEVER-FIVE_LOAVES_AND_TWO_FISH, BELIEVER-FOUR_HORSEMEN, BELIEVER-GENESIS
[MiracleRewardTest] SECOND boss ids=...; containsSelected=False
[MiracleRewardTest] NORMAL candidates: BELIEVER-PRICE[Uncommon], BELIEVER-DEDICATION[Common],
  BELIEVER-VERDICT[Uncommon]; miracleCount=0
[MiracleRewardTest] Omniscience provider sees 1: BELIEVER-FALL_OF_JERICHO; selectedPresent=True
[MiracleRewards] Rare reward found but no unowned Miracle is available; keeping the original card reward.
[MiracleRewardTest] FULL-COLLECTION fallback candidates=BELIEVER-ARMOR_BREAK, BELIEVER-COMPOSURE,
  BELIEVER-GUARDIAN; miracleCount=0
[MiracleRewardTest] reward pipeline test completed.
```

### 3.3 对应测试项

| 测试 | 结果 |
|---|---|
| Boss 稀有奖励候选展示 | 3/3 为神迹 |
| 选择神迹后登记 | `Added`，`owned=True` |
| 神迹是否进入主卡组 | 否，deck 10→10 |
| 重复选择 | `AlreadyOwned`，owned 仍为 1，deck 不变 |
| 多次获取 | 第二份 Boss 奖励排除已拥有神迹 |
| 普通白/蓝奖励 | 普通 Common/Uncommon，miracleCount=0 |
| 全知全能读取来源 | provider 能看到已选择神迹 |
| 无可用神迹兜底 | 保留 3 张普通卡，无空选项/崩溃 |

## 4. Build / Publish / 部署

- `dotnet build`：**0 warning / 0 error**
- `dotnet publish`：**exit 0**
- 部署：
  - `Believer.dll`：1,912,320 B
  - `Believer.pdb`：82,136 B
  - `Believer.pck`：392,604 B
  - `Believer.json`：366 B
- 无测试标记正常启动通过。
- 启动日志确认补丁目标：
  ```text
  [Patches] Miracle reward: generation postfix=1, deck-add prefix=1, targetsFound=True.
  ```

## 5. 修改文件

- `BelieverCode/Patches/MiracleRewardAcquisition.cs`（+ `.uid`）
  - `MiracleRareRewardGenerationPatch`
  - `MiracleRewardDeckAddPatch`
- `BelieverCode/Dev/BelieverRewardPipelineTest.cs`（+ `.uid`，dev-only 测试）
- `BelieverCode/MainFile.cs`
  - 增加补丁附加状态日志
- 本报告

未修改：

- 12 张神迹的效果；
- 神性获取/消耗/上限；
- 普通卡牌、初始卡组、遗物、敌人；
- 全知全能持续时间、费用、抽牌数、清 Debuff、不扣血；
- 其他角色的卡牌奖励逻辑。

## 6. 尚未完成 / 限制

1. **未在真实 Boss 奖励 UI 中逐步点击截图**：当前环境无法可靠自动进入真实 Boss 战斗并完成奖励选择。验证使用隔离测试 RunState + 真实 `CardFactory` / `PileType.Deck` 添加路径，不是 UI 截图。
2. 真实游戏内 Boss 奖励的卡面显示、点击手感、奖励结束动画仍需实机确认。
3. 该修复先覆盖“BossEncounter 稀有奖励”和“实际出现 Rare 的其他奖励”。如果后续给信徒卡池加入真正的 Rare 卡，普通奖励中的 Rare 会被替换；Elite 的普通白/蓝候选不受影响。
4. `NCardRewardSelectionScreen` 是否接受 `CardRarity.Token` 的神迹卡面未在真实 UI 中截图验证；测试确认奖励数据与选择添加路径正常，但 UI 仍待实机。
5. 神迹在测试 RunState 中通过 `RunState.CreateCard` 创建为奖励展示实例；选择后不会进入牌组或永久卡组。真实存档中的 RunState 级临时卡清理需实机存档验证。

# M3 初始遗物「残缺圣印」· 设计与实现说明

## 1. 目标

为「信徒」添加专属初始遗物 **残缺圣印 / Incomplete Seal**：每回合开始时获得 1 点神性（不超过 7 点上限）。

## 2. 实现方式

| 需求 | 实现 |
|---|---|
| 设为初始遗物 | `Believer.StartingRelics` 改为 `ModelDb.Relic<IncompleteSeal>()`（替换模板占位遗物 BurningBlood） |
| 每回合开始获得 1 神性 | 遗物重写 `AbstractModel.AfterPlayerTurnStart(PlayerChoiceContext, Player)`：战斗内每位玩家回合开始时触发一次（在能量重置与起手抽牌之后） |
| 不超过 7 点上限 | 调用 M1 的 `BelieverDivinityResource.Gain(1)`，其 `Amount` setter 自带 `Math.Clamp(value, 0, MaxAmount)`（MaxAmount = 7） |
| 复用 M1 资源系统 | 通过 `player.GetResource<BelieverDivinityResource>()` 取同一份 per-combat 实例，不新建变量；`AmountChanged` → HUD 与手牌刷新沿用 M1/M2 链路 |
| 只在战斗内玩家回合触发 | `AfterPlayerTurnStart` 是战斗专属 hook（`Hook.AfterPlayerTurnStart` 由 `CombatManager.SetupPlayerTurn` 调用），不会在地图、奖励界面或敌方回合触发；`player != Owner` 保证联机时只有遗物拥有者触发 |
| 本地化 | `Believer/localization/eng/relics.json`：`BELIEVER-INCOMPLETE_SEAL` 的 title / description / flavor，ModAnalyzers 的 STS001 校验通过 |
| 正常显示/触发 | 继承模板 `BelieverRelic`，图标走 mod 资源回退（占位 relic 图）；`RelicRarity.Starter` 使其只会作为初始遗物获得，不会进入随机遗物奖励（奖励只掷 Common/Uncommon/Rare）；触发时调用 `RelicModel.Flash()` 反馈（满 7 点时不再闪光） |

## 3. 新增 / 修改文件

新增：

```
BelieverCode/Relics/IncompleteSeal.cs        残缺圣印遗物（Starter，回合开始 +1 神性）
Believer/localization/eng/relics.json       遗物 title / description / flavor
docs/M3-IncompleteSeal.md                    本说明
```

修改：

```
BelieverCode/Character/Believer.cs           StartingRelics 换成 IncompleteSeal（并调整 using）
```

（`BelieverRelic` / `BelieverRelicPool` 沿用模板，未改动；遗物通过基类的 `[Pool(typeof(BelieverRelicPool))]` 归入信徒遗物池。）

## 4. 关键 API（已按实际源码核实）

- `RelicModel`：`public abstract RelicRarity Rarity { get; }`、`public override bool ShouldReceiveCombatHooks => true;`、`public void Flash()`。
- `AbstractModel.AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)`：战斗专属、每位玩家回合开始一次；`Hook.AfterPlayerTurnStart` 在 `CombatManager.SetupPlayerTurn` 末尾（抽牌之后）逐模型调用。
- `RelicRarity.Starter`：初始遗物；`RelicFactory.RollRarity` 只返回 Common/Uncommon/Rare，故不会出现在随机遗物奖励里。
- 自定义角色通过 BaseLib 的 `AddCustomCharacters`（`ModelDb.AllCharacters` 的后缀补丁）进入游戏内容列表，因此信徒的遗物池会被纳入 `ModelDb.AllRelicPools`，`RelicModel.Pool` 可正常解析。

## 5. 需要游戏内验证的项目

1. 新开一局（角色选择信徒）→ 初始遗物栏出现「残缺圣印」，悬停显示本地化名称/描述/flavor。
2. 战斗第 1 回合及之后每回合开始，神性计数 +1（0→1→…→7）。
3. 到达 7/7 后继续回合开始，数值保持 7，遗物不再闪光。
4. 地图节点/奖励界面/敌方回合不会增加神性。
5. 神性满 7 时，全知全能卡在回合开始的神性刷新后变为可打（金色高亮）。
6. 单人及（可选）联机各自只在自己回合触发一次。

## 6. 验证记录

- `dotnet build Believer.csproj`：成功，0 warning / 0 error（含 ModAnalyzers 本地化检查）。
- `dotnet publish Believer.csproj -p:PublishProfile=FolderProfile`：exit 0，dll/pdb/pck 部署到 `<游戏目录>/mods/Believer/`。
- 启动游戏冒烟测试：mod 初始化成功、`ModelDb.Preload` 正常解析遗物图标（占位图回退）、日志无异常。

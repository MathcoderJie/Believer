# M10：信徒神性光球可视化系统

> 本阶段只实现神性可视化。未修改神性获取/消耗/上限、卡牌、12 张神迹、遗物、初始卡组或全知全能逻辑。
> 光球是独立节点，不画进角色 PNG。

## 1. 数据源与同步机制

真实数据源只有一处：

- `BelieverCode/Resources/BelieverDivinityResource.cs`
- 数值属性：`Amount`
- 实际上限：`MaxAmount = 7`
- 变化事件：BaseLib `CustomResource.AmountChanged`（在 `Amount` setter 内触发）

所有路径最终都经过 `Amount`：

- `Gain()` → `Amount += amount`（由本类 clamp 到 0..7）
- `TrySpend()` → `Amount -= amount`
- `Amount = ...`
- 卡牌、遗物、回合效果、控制台命令等

因此没有复制第二份神性数值。`BelieverDivinityOrbLayer` 只订阅真实 `BelieverDivinityResource.AmountChanged`，收到 `(old, new)` 后按 `new` 重排光球。

接入路径：

```text
NCreature._Ready (Harmony postfix)
  → 仅当 Entity.Player.Character is Believer
  → Visuals.GetNodeOrNull<Node2D>("BelieverEffectLayer")
  → 添加 BelieverDivinityOrbLayer
  → Initialize(player)
  → player.GetResource<BelieverDivinityResource>()
  → resource.AmountChanged += OnAmountChanged
```

- 首次绑定后立即 `Sync(resource.Amount)`，因此战斗开始 0/7 时没有光球。
- 如果资源在 `_Ready` 时尚未就绪，会以 0.1 秒间隔重试最多 10 次；不是每帧轮询。
- `_ExitTree` 解除事件订阅，避免战斗结束/节点销毁后的残留订阅。

## 2. 光球外观与运动

- 贴图：`Believer/images/characters/believer_orb.png`（内嵌 DLL）
  - 柔和白金色核心
  - 淡金色光晕
  - 白色中心
  - 软边缘，不是硬圆
- 每颗光球是一个轻量 `Sprite2D`（没有独立粒子系统）。
- 最大 7 颗，环绕中心在角色头顶上方：
  - 椭圆轨道，中心 Y=-306，横向半径约 102 px，纵向约 28 px
  - 不同 index 使用不同相位
  - 缓速环绕（约 0.32 rad/s，按反馈提高；约 19.6 秒一圈）
  - 各自有轻微上下浮动和交错高度
  - 不会改变角色位置、碰撞或 Bounds
- 光球层挂在 `BelieverEffectLayer` 下（ZIndex 2），位于角色前景；轨道在头顶/头部上方，避免覆盖胸口圣徽和主要轮廓。
- 角色 Idle 只移动 Sprite2D 本体，光球层独立环绕；两者互不修改基准变换。

## 3. 生成/消失与数量

### 增加

- `AmountChanged` 后按新值补齐节点；
- 新光球 0.28 秒内从 0.18 倍缩放扩张到正常大小；
- 初始带短暂亮度提升，然后回归正常；
- 立即加入当前环绕轨道。

### 减少

- 多余光球进入 `Retiring` 状态；
- 0.22 秒内收缩、变暗、淡出；
- 结束后 `QueueFree`；
- 实际神性数值不等待动画，先更新真实资源。

### 快速变化

- 节点列表按目标数量复用；
- 目标增加时重新激活仍在 retiring 的节点；
- 目标减少时只 retire 尾部节点；
- 数量以 `ActiveCount`（非 retiring）核对；
- 没有每帧创建/销毁，也没有每帧读取资源。

## 4. 死亡、复活与清理

- `NCreature.SetAnimationTrigger("Dead")`：`BelieverDivinityOrbLayer.BeginDeath()` 让所有光球 retire；
- `"Revive"`：`EndDeath()` 重新按当前真实 Amount 同步；
- 战斗结束/场景退出：`NCreatureVisuals` 随角色销毁，`_ExitTree` 解除 `AmountChanged` 订阅；
- 不会永久绑定到某个屏幕坐标，层始终挂在角色视觉根节点。

## 5. 游戏内测试

测试标记：`user://believer_orb_test.flag`（测试后删除）。
测试使用**独立的测试 `BelieverDivinityResource`**，用于验证事件/重排/数量；没有伪造真实战斗数据。

截图（游戏内 dev 展示层，深色测试背景）：

![divinity orbs](images/M10.1-divinity-orbs-higher.png)

展示 0/7、1/7、4/7、7/7 四份角色：

- 0/7：0 颗
- 1/7：1 颗
- 4/7：4 颗
- 7/7：7 颗

动态快速增减日志（实际输出）：

```text
divinity 7 -> 3  sync amount=3 desired=3 active=3 totalNodes=7
divinity 3 -> 0  sync amount=0 desired=0 active=0 totalNodes=3
divinity 0 -> 7  sync amount=7 desired=7 active=7 totalNodes=7
divinity 7 -> 1  sync amount=1 desired=1 active=1 totalNodes=7
divinity 1 -> 0  sync amount=0 desired=0 active=0 totalNodes=1
```

每项 `active` 都与真实 `amount` 一致；`totalNodes` 可能暂时大于 active，是正在播放消失动画的 retiring 节点。

## 6. Build / Publish / 部署

- `dotnet build Believer.csproj`：**0 warning / 0 error**
- `dotnet publish ... -p:PublishProfile=FolderProfile`：**exit 0**
- 部署：
  - `Believer.dll`：1,902,592 B
  - `Believer.pdb`：79,944 B
  - `Believer.pck`：392,268 B
  - `Believer.json`：366 B
- 无测试标记正常启动通过，不加载测试展示层。

## 7. 修改文件

- `BelieverCode/Character/BelieverDivinityOrbs.cs`（新增）
  - `BelieverDivinityOrb`
  - `BelieverDivinityOrbLayer`
  - `BelieverDivinityOrbAttachPatch`
  - `BelieverDivinityOrbDeathPatch`
- `BelieverCode/Character/BelieverDivinityOrbs.cs.uid`
- `Believer/images/characters/believer_orb.png`（新增）
- `BelieverCode/Dev/BelieverOrbTest.cs` / `.uid`（dev 展示与动态同步测试）
- `Believer.csproj`（orb PNG EmbeddedResource）
- `docs/images/M10.1-divinity-orbs-higher.png`
- 本报告

已有未提交工作全部保留；未执行 reset/clean/stash。

## 8. 尚未验证 / 限制

1. **真实战斗内未验证**：当前环境无法自动完成菜单/角色选择/进入战斗输入。截图使用游戏内 dev 展示层和测试资源；`NCreature._Ready` 实际绑定路径、真实卡牌/遗物变化、战斗结束清理尚未进入真实战斗逐项截图。
2. 0/1/4/7 截图中是测试资源，不是当前 run 的真实 `BelieverDivinityResource`；真实资源同步代码已经接入，但没有战斗内实际截图。
3. 角色 PNG 仍是临时占位美术，不是最终正式美术。
4. 光球轨道位置/层级需要在实际战斗镜头下再检查；当前是相对角色视觉根节点的局部坐标。
5. 光球消失只做收缩+淡出，没有独立金色粒子爆发；符合本阶段轻量要求，但不是最终全知全能演出。
6. 未实现全知全能的升空、七重神环、圣印、爆发；这些留给后续阶段。

# M4 神迹收藏系统 · 设计与实现说明

> 本阶段不实现 12 张神迹卡牌、不开发奖励界面、不生成临时神迹牌；只建立"本局神迹收藏"数据模块与
> 其存档/读档能力，并为后续全知全能模块预留查询接口。

## 1. 数据模型

| 类型 | 作用 |
|---|---|
| `MiracleId` | 神迹的稳定唯一 ID（= 神迹卡牌的 `ModelId.Entry`，如 `BELIEVER-DEV_MIRACLE_TEST_CARD`）。仅校验格式（非空、有界、无空白），存在性由 Catalog 判定。 |
| `MiracleCatalog` | 从 `ModelDb.AllAbstractModelSubtypes` 自动收集所有**已存在**的具体 `MiracleCard` 子类，得到 id → 卡牌的映射。未来 12 张神迹卡牌只要继承 `MiracleCard` 就自动进入 Catalog，无需改本文件。这是"不存在 ID 安全处理"的判定依据。 |
| `MiracleCollection` | 单局收藏数据：`TryAdd` / `Has` / `OwnedIds` / `Count` / `Clear`、`Changed` 事件；拒绝重复与不存在的 id；枚举顺序按 ID 排序，保证存档稳定。 |
| `MiracleCollectionStore` | 单局收藏管理器 + 存档接入 + 查询接口：`Find/GetOrCreate(IRunState/Player)`、`TryAdd/Has/Clear(player, id)`、`GetOwnedIds(runState)`、`GetOwnedMiracleCards(runState/player)`（M5 全知全能查询接口）。 |

## 2. 关键规则如何满足

1. **只记录本局获得的神迹**：`MiracleCollectionStore` 以 `ConditionalWeakTable<IRunState, MiracleCollection>` 按 RunState 实例保存；新开一局 = 新 RunState = 空收藏，不会继承上一局。
2. **唯一稳定 ID**：`MiracleId` 存 `ModelId.Entry`（BaseLib 会加 `BELIEVER-` 前缀），类名不变则 ID 稳定；存档中只存该字符串。
3. **不可重复**：`MiracleCollection.TryAdd` 用 `HashSet<MiracleId>` 去重，重复返回 `AlreadyOwned`，不抛异常。
4. **增 / 查 / 枚举 / 清空**：`TryAdd`、`Has`、`OwnedIds`、`Clear`（另有 `Count` 与 `Changed`）。
5. **新开一局为空**：新的 `IRunState` 没有对应集合，首次访问创建空集合。
6. **存档/读档恢复**：通过 BaseLib 的扩展存档 API
   `ExtendedSaveTypes.RegisterSavedValue<IRunState, List<string>>` 把收藏写进游戏原生 `SerializableRun`
   的扩展数据（属性名 `save_dict_ListString` 中的 `Believer-miracle-collection` 键）。覆盖：
   保存游戏、读档继续、联机中途加入时 `SerializableRun` 的网络序列化；BaseLib 同时处理了
   `CanonicalizeSave` / `Anonymized` 等存档派生路径。读取时对格式错误/已不存在的 ID 跳过并告警。
7. **与主卡组分离**：模块只存 ID 字符串，从不创建卡牌，也不触碰抽牌堆/弃牌堆/手牌/牌组。
8. **为 12 张神迹预留兼容接口**：任何继承 `MiracleCard` 的新卡自动获得合法 ID；M5 用
   `MiracleCollectionStore.GetOwnedMiracleCards(...)` 即可拿到本局已拥有神迹的卡牌模型。

## 3. 存档机制说明（重要）

- 游戏的原生 `SerializableRun` 只序列化固定字段，mod 无法直接加字段；BaseLib 提供
  `BaseLib.Patches.Saves.ExtendedSaveTypes` 把自定义数据挂到 `SerializableRun` 的 JSON 属性上，
  并在读档时回调 setter。
- 注册必须在 mod 初始化阶段完成（BaseLib 在 `MegaCritSerializerContext.SerializableRunPropInit`
  首次执行时收集扩展属性；本 mod 的 `MainFile.Initialize` 早于该时机）。
- 存储内容为 `List<string>`（神迹 ID 列表，ordinal 排序）。空收藏不写入任何数据。
- 旧存档（M1–M3 时期）没有该数据，读档时收藏为空，向后兼容。

## 4. 新增 / 修改文件

新增：

```
BelieverCode/Miracles/MiracleId.cs               稳定唯一 ID 值类型
BelieverCode/Miracles/MiracleCatalog.cs          已存在神迹 ID 目录（自动发现 MiracleCard）
BelieverCode/Miracles/MiracleCollection.cs       单局收藏数据 + AddResult 枚举
BelieverCode/Miracles/MiracleCollectionStore.cs  按 RunState 的管理器 + 存档接入 + M5 查询接口
BelieverCode/Dev/MiracleConsoleCmd.cs            【开发调试】miracle 控制台命令
docs/M4-MiracleCollection.md                     本说明
```

修改：

```
BelieverCode/MainFile.cs  初始化时调用 MiracleCollectionStore.RegisterSave()
```

未修改：M1 神性、M2 全知全能/神迹接口、M3 初始遗物、火堆祈祷/神谕/普通卡牌/遗物、金色稀有卡奖励。

## 5. 测试方法

前置：游戏以 mod 模式启动（modded 下开发者控制台允许调试命令），进入一局「信徒」游戏。

1. 打开开发者控制台（游戏内控制台），输入：
   - `miracle known` → 列出当前 build 中存在的神迹 ID（当前只有 M2 的 dev 测试神迹
     `BELIEVER-DEV_MIRACLE_TEST_CARD`）。
   - `miracle add BELIEVER-DEV_MIRACLE_TEST_CARD` → `Added miracle ...`
   - `miracle add BELIEVER-DEV_MIRACLE_TEST_CARD` → `Already owned`（防重复）。
   - `miracle add NOT_A_REAL_MIRACLE` → 拒绝并提示用 `miracle known`（不存在 ID 安全处理）。
   - `miracle has BELIEVER-DEV_MIRACLE_TEST_CARD` → `Owned: ...`
   - `miracle list` → 输出已收藏 ID。
   - `miracle clear` → 清空。
2. 存档/读档恢复：`miracle add ...` → 保存并退出 → 继续该局 → `miracle list` 应仍显示该 ID。
3. 新开一局：进入新的一局（不必先退出游戏）→ `miracle list` 应为空（不继承上一局）。
4. 与牌组分离：`miracle list` 添加后，打开牌组/抽牌堆/弃牌堆检查，卡牌数量与内容不应变化。
5. 联机（可选）：主机的控制台命令经原生 `ConsoleCmdGameAction` 同步，所有端收藏一致。

## 6. 验证记录

- `dotnet build Believer.csproj`：成功，0 warning / 0 error。
- `dotnet publish Believer.csproj -p:PublishProfile=FolderProfile`：exit 0，部署成功
  （Believer.dll / Believer.pdb / Believer.pck 均更新，dll 中已确认包含 M4 类型与 `miracle` 命令）。
- 启动冒烟（经 Steam 启动到主菜单后关闭）：mod 初始化成功，日志关键行：
  ```
  [INFO] [Believer] [Miracles] List<string> already supported by the run serializer: True
  [INFO] [Believer] [Miracles] Registered run-save data 'Believer-miracle-collection'.
  ```
  无异常 / 无 Harmony 失败。说明：`SerializableRun` 的扩展属性会在游戏第一次序列化/反序列化 run
  存档时构建（无进行中的存档时启动日志中不会出现），因此"存档→读档恢复"需要在游戏内按第 5 节步骤验证。


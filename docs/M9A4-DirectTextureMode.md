# M9-A4：信徒直接贴图显示模式验证

> 本阶段将信徒角色视觉从 Spine 角色切换为“普通透明 PNG + Sprite2D”显示方式。
> 未修改卡牌、神性、12 张神迹、遗物、初始卡组和全知全能逻辑；未覆盖原版 Ironclad/Silent 资源。

## 1. 修改前检查

- 分支：`main`
- HEAD：`b740bee`（A2.1 checkpoint）
- 工作区原有 A2.3/A2.4/A3 未提交修改全部保留，未执行 reset/clean/stash。
- 参考图：
  - `Assets/Reference/神秘金饰长袍祭司姿态设定集.png` 存在
  - `Assets/Reference/暗影圣职者角色设定稿.png` 存在（当前模型参考）
  - `Assets/Reference/Believer_Model_Reference.png` **不存在**

## 2. 接入方式调查结果

BaseLib 的 `NodeFactory<NCreatureVisuals>` 支持直接由 `Texture2D` 生成 `NCreatureVisuals`：

- `NCreatureVisualsFactory.CreateBareFromResource` 检测到 `Texture2D` 时：
  - 创建 `NCreatureVisuals`；
  - 创建 `Bounds`；
  - 创建 **`Sprite2D`**（而不是 SpineSprite）；
  - 将纹理赋给 Sprite2D。
- 因此普通 PNG 可直接走现有角色视觉接口，不需要 SpineSprite，也不需要修改游戏渲染系统。

## 3. 单张角色 PNG

原始参考图是多姿态设定集，不能整张作为战斗角色图使用。当前环境没有专业绘图/图像生成工具，因此本阶段制作的是**临时单角色 PNG 占位图**：

- 从 `暗影圣职者角色设定稿.png` 的**右侧面单人图**裁切；
- 去掉深蓝背景与面板标题，得到透明 RGBA；
- 缩放到游戏目标高度约 **266 px**（实际裁切内容约 180×266）；
- 文件：`Believer/images/characters/believer_direct.png`

**这不是最终手绘美术**；它是为验证直接贴图显示链路制作的临时占位图。最终仍需美术提供正式单人透明 PNG。

角色图特征：尖顶兜帽、深色面部、象牙白长袍、金色边缘、层叠肩披；面部无五官；透明背景、无文字/边框。

## 4. 代码接入

新增：

- `BelieverCode/Character/BelieverDirectVisuals.cs`
  - 从 DLL 内嵌 PNG 读取字节；
  - `Image.LoadPngFromBuffer` → `ImageTexture.CreateFromImage`；
  - `NodeFactory<NCreatureVisuals>.CreateFromResource(texture)`；
  - 返回 BaseLib 生成的 `NCreatureVisuals`（Sprite2D）。
- `BelieverCode/Dev/BelieverDirectTextureTest.cs`
  - dev-only 测试入口；
  - 标记：`user://believer_direct_texture_test.flag`；
  - 生成三份直接贴图角色 + 一份正式 `Believer.CreateCustomVisuals()` 结果，验证透明显示和正式接口。

修改：

- `BelieverCode/Character/Believer.cs`
  - 新增 `CreateCustomVisuals()` 覆盖：
    ```csharp
    public override NCreatureVisuals? CreateCustomVisuals()
        => BelieverDirectVisuals.Create();
    ```
  - 若 PNG 加载失败返回 null，仍可回退 placeholder 逻辑。
- `Believer.csproj`
  - 将 PNG 作为 EmbeddedResource 打进 DLL；PNG 同时保存在信徒资源目录。
- `Believer/images/characters/believer_direct.png`（临时占位图）。

`Believer` 不再通过 Ironclad Spine 场景作为战斗视觉；正式战斗视觉接口会走直接 PNG。

## 5. 游戏内验证

测试标记：`user://believer_direct_texture_test.flag`（测试后删除）。

实际游戏日志：

```text
[BelieverDirect] loaded PNG 180x266
[BaseLib] Creating NCreatureVisuals from Texture2D
[BelieverDirect] NCreatureVisuals created: Node2D
...
[BelieverDirectTest] formal Believer.CreateCustomVisuals => Node2D
[BelieverDirectTest] direct PNG overlay added
```

截图：[docs/images/M9A4-direct-texture-formal.png](images/M9A4-direct-texture-formal.png)

观察到：

- PNG 正常解码、显示。
- 角色透明背景正常，没有黑色/深蓝底块。
- Sprite2D 路径正常生成，无 SpineSprite 依赖。
- 正式 `Believer.CreateCustomVisuals()` 返回非空 Node2D，说明正式角色接口可以走直接贴图。
- 原版 Ironclad/Silent 资源未被修改或覆盖。
- 无测试标记正常启动不显示测试角色。

## 6. Build / Publish / 部署

- `dotnet build Believer.csproj`：**0 warning / 0 error**
- `dotnet publish ... -p:PublishProfile=FolderProfile`：**exit 0**
- 部署：
  - `Believer.dll`：1,807,360 B（包含 PNGCreatureVisuals 逻辑与内嵌 PNG）
  - `Believer.pdb`：59,816 B
  - `Believer.pck`：344,416 B
  - `Believer.json`：366 B

## 7. 尚未完成 / 已知限制

1. **不是最终美术**：`believer_direct.png` 是从设定稿裁切、去背景得到的临时占位图，不是正式手绘/生成的最终角色 PNG；需要美术或图像生成工具输出正式单人透明图。
2. **静态贴图，无动画**：本版只验证静态显示；待机、攻击、施法、受击、死亡动画尚未接入。后续可以扩展为多张姿态图片切换，或保留静态 + 节点浮动/闪白/震动。
3. **未实际进入战斗场景**：当前环境无法自动完成菜单/角色选择/进入战斗的输入操作；已通过游戏内 dev 覆盖层验证直接 PNG 的加载、透明显示和正式 `CreateCustomVisuals()` 接口，但尚未截取真实战斗画面。
4. **战术位置/缩放仍需战斗内确认**：BaseLib 自动生成的 Bounds 基于 PNG 尺寸；正式战斗中的站位、血条/意图位置需实机确认。
5. **角色图是侧身设定稿裁切**，透明边缘存在轻微抠图痕迹；正式版需重绘干净的 alpha 边缘。
6. 全知全能特效未开始；角色 PNG 与未来特效保持分离。

## 8. 下一步

- 由美术/图像生成工具按参考图输出正式单角色透明 PNG（优先 2× 分辨率，角色居中、透明边距合理）。
- 替换 `believer_direct.png` 后重新 Build/Publish。
- 实机进入战斗检查角色大小、锚点、站位与透明背景；如需动画，再逐步增加攻击/施法/受击图片或节点动画。
- 全知全能的光球、升空、七重神环、地面圣印、白金爆发作为独立节点叠加，不画进普通角色 PNG。

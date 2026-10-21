# Believer Card Art Pack — prototype v1

包含 40 张普通卡、12 张神迹基础图，以及 12 张神迹强化形态图（共 64 个 PNG）。

## 安装
将本包中的 `Assets/CardArt/` 复制到项目 `C:\STS2ModDev\Believer\Assets\CardArt\`。

## 重要说明
- 这些是从本轮生成的卡牌总览图中裁切出的**概念原型图**，并非逐张高分辨率精绘原稿。为了方便接入，已放大到 512×512；放大不会恢复原始细节。
- `05_Miracles_Enhanced` 是基础神迹图的程序化金色强化变体，适合作为占位版本；之后可以逐张替换为专门绘制的强化图。
- `Source_ContactSheet.png` 保留原始总览图供对照。
- `manifest.csv` 列出卡牌 ID、中文名、稀有度与相对路径。
- 本包只提供图像资源，不会自动改动游戏代码、卡牌定义或 Mod 的加载映射。需要 DSH 将路径绑定到实际卡牌资源，并在游戏内验证。

## 目录
- `01_Basic/`：基础卡 10 张
- `02_White/`：白卡 15 张
- `03_Blue/`：蓝卡 15 张
- `04_Miracles/`：神迹普通形态 12 张
- `05_Miracles_Enhanced/`：神迹强化形态 12 张

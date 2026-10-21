# Agnes 生图管线（Believer 卡图）

用 Agnes AI 图像生成 API 为 Believer 的 64 张卡批量生成正式卡图：
**big 1000×760 / small 250×190 RGB PNG**，输出路径与游戏 `card_portraits` 契约一致。

- 通用客户端：`agnes_client.py`（文生图 / 图生图 / 多图合成）
- 卡图批处理：`card_art.py`（读 `docs/M10.2.1-CardArt-Spec.csv`，64 个任务）
- 画风与提示词：`believer_style.py`
- 独立验收：`verify_pipeline.py`

完整说明见 [docs/M10.5-AgnesImagePipeline.md](../../docs/M10.5-AgnesImagePipeline.md)。

## 一次性配置

```powershell
# 1) 复制模板并填入密钥（.env 已被 .gitignore 忽略，不要提交）
Copy-Item Tools\AgnesImage\.env.example Tools\AgnesImage\.env
notepad Tools\AgnesImage\.env

# 2) 用 DSH 内置 Python（PATH 里的 python 可能是 Windows 应用商店占位符）
$PY = "C:\Users\Administrator\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe"
& $PY -c "import PIL; print(PIL.__version__)"
```

`.env` 内容：

```dotenv
AGNES_API_KEY=你的密钥
AGNES_MODEL=agnes-image-2.5-flash
```

密钥解析优先级：显式参数 > `AGNES_API_KEY` 环境变量 > `Tools/AgnesImage/.env` > 仓库根 `.env`。

## 常用命令

```powershell
cd Tools\AgnesImage

# 只列 64 个任务与提示词，不调用 API、不写文件
& $PY card_art.py --all --dry-run

# 生成一张到暂存目录（Tools/AgnesImage/out/card_art/）
& $PY card_art.py --only BELIEVER-FOCUS --staging

# 生成若干张（逗号分隔，支持 card_id 或 slug），并出接触表
& $PY card_art.py --only BELIEVER-STRIKE_BELIEVER,enhanced_let_there_be_light --sheet

# 全量 64 张（约 13s/张，并发 2 → 约 8-15 分钟；免费额度按平台政策）
& $PY card_art.py --all --concurrency 2 --sheet

# 直接写入游戏卡图目录（会覆盖同名 PNG，确认后再用）
& $PY card_art.py --all --install

# 单张重生成
& $PY card_art.py --only focus --force
```

## 输出契约

| 用途 | 路径（`--install`） | 尺寸 |
|---|---|---|
| 大卡图 | `Believer/images/card_portraits/big/<slug>.png` | 1000×760 RGB |
| 小卡图 | `Believer/images/card_portraits/<slug>.png` | 250×190 RGB |
| 暂存（默认） | `Tools/AgnesImage/out/card_art/{big,small}/<slug>.png` | 同上 |

`slug` = 卡牌 ID 去掉 `BELIEVER-` 前缀并小写，例如
`BELIEVER-STRIKE_BELIEVER` → `strike_believer`，
`BELIEVER-ENHANCED_LET_THERE_BE_LIGHT` → `enhanced_let_there_be_light`。

生成结果会先自动去掉 Agnes 偶发的浅灰留白条（letterbox），再 cover-fit 到目标尺寸；
每张图的 `original_size` / `content_bbox` / `bars_trimmed` 都记录在
`out/manifest.jsonl` 中。

## 常见问题

| 现象 | 处理 |
|---|---|
| `HTTP 401 无效的令牌` | 密钥属于国际站，客户端会自动切到 `apihub.agnes-ai.com`；若仍 401 请重新生成密钥 |
| `HTTP 503 image queue is full` | 平台图像队列繁忙（瞬时），客户端自动重试 5 次；批量时把并发降到 1-2 |
| `HTTP 429` | 超出 RPM（免费档约 20 RPM），用 `--rpm 15` 或降低并发 |
| 图生图一直 503 | 平台图生图队列当前不可用；先用纯文生图，恢复后再用 `--ref` |
| 生成的图带灰边 | 已自动裁切；若仍出现，用 `--force` 重生成 |
| PATH 里 python 无法运行 | 使用上面的 `$PY` 全路径（DSH 内置 Python 3.12 + Pillow） |

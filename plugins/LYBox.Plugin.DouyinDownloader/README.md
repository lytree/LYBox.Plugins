# 抖音下载器 (LYBox Plugin)

LYBox 原生插件 — 1:1 移植自开源项目 [`douyin-downloader`](https://github.com/jiji262/douyin-downloader) (MIT License)。

## 已实现

| 模块 | 对应原项目 | 状态 |
|------|-----------|------|
| URL 解析（video / note / user / collection / mix / music / live / live_replay / 短链） | `core/url_parser.py` + `utils/validators.py` | ✅ |
| 抖音 API 客户端（详情 / 作品 / 喜欢 / 合集 / 收藏 / 收藏合集 / 音乐 / 搜索 / 热搜 / 评论 / 直播） | `core/api_client.py` | ✅ 核心端点 |
| 签名 | `utils/xbogus.py` | ✅ **1:1 移植**（RC4 + MD5 + Base64）+ 第三方签名服务封装 |
| 默认 query + msToken + 风控重试 + login_required | `auth/ms_token_manager.py` + `core/api_client.py` | ✅ |
| Cookie 管理（保存 / 校验 / 黑名单） | `auth/cookie_manager.py` | ✅ |
| 单视频 / 图文下载（无水印优先 + 图集） | `core/video_downloader.py` | ✅ |
| 用户主页批量（post / like / mix） | `core/user_downloader.py` + `core/user_modes/*` | ✅ |
| 当前账号收藏作品 / 收藏合集（self-only） | `core/user_modes/collect_strategy.py` + `collect_mix_strategy.py` | ✅ |
| 合集下载 | `core/mix_downloader.py` | ✅ |
| 媒体下载（HTTP + Range 断点续传 + 完整性校验） | `core/downloader_base.py` | ✅ |
| 速率限制 / 重试 / 任务队列 | `control/*` | ✅ |
| 文件命名模板 (`{date}_{title}_{id}` 等) | `utils/naming.py` | ✅ |
| SQLite 历史/去重/增量 | `storage/database.py` (aweme 表) | ✅ |
| Web 控制台 (REST API + 内嵌 HTML) | `server/app.py` | ✅ 简化版 |
| Avalonia 页面 (Submit / Jobs / History / Settings / Login) | — | ✅ |
| 进度回调 / Webhook | `utils/notifier.py` | ⛔ 暂留 TODO |
| 直播录制 (FLV/HLS) + 直播回放 | `core/live_downloader.py` + `core/live_replay_downloader.py` | ✅ |
| 浏览器兜底 (Playwright) | `core/api_client.collect_user_post_ids_via_browser` | ✅ 反射调用 (无需强依赖) |
| 视频转写 (Whisper) | `core/transcript_manager.py` | ⛔ 暂留 TODO |

## 配置项 (Settings 页面)

```jsonc
{
  "signerEndpoint": "",            // 第三方签名服务地址,留空则使用本地 1:1 移植的 XBogus
  "downloadPath": "Downloaded",
  "proxy": "",                     // 例如 http://127.0.0.1:7890
  "concurrency": 5,
  "retryTimes": 3,
  "database": true,
  "databasePath": "dy_downloader.db",
  "enableIncremental": true,
  "enableWebConsole": true,
  "webConsoleHost": "127.0.0.1",
  "webConsolePort": 8765,
  "fileTemplate": "{date}_{title}_{id}",
  "folderTemplate": "{date}_{title}_{id}",
  "webhookUrl": "",
  "enableTranscript": false,
  "transcriptApiKey": "",
  "transcriptModel": "gpt-4o-mini-transcribe"
}
```

## Web 控制台路由

```
GET  /                       — 控制台首页
GET  /api/v1/health          — 健康探针
GET  /api/v1/jobs            — 任务列表
GET  /api/v1/jobs/{id}       — 单任务
DELETE /api/v1/jobs/{id}     — 移除任务
POST /api/v1/download        — {url, mode, number}
GET  /api/v1/settings        — 获取设置
POST /api/v1/settings        — 更新设置
```

## 第三方签名服务契约（可选）

```
POST {signerEndpoint}
Content-Type: application/json
{ "url": "https://www.douyin.com/aweme/v1/web/aweme/post/?...", "ua": "..." }

→ 200
{ "signed_url": "https://...?X-Bogus=...", "x_bogus": "...", "user_agent": "..." }
```

未配置时自动回退到本地 XBogus（与原项目同算法）。

## 反爬风险与免责

- 抖音 web API 风控严格,签名 / msToken 失效时会返回 403 / 2483 / 空 200。
- 插件内置重试、空 200 检测、login_required 抛错,但实际可用性取决于签名服务 / Cookie。
- 行为合规性与用户责任：遵守抖音平台规则与当地法律法规。

## 许可

本插件 MIT,基于 [`douyin-downloader`](https://github.com/jiji262/douyin-downloader) (MIT) 移植。

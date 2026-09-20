# LYBox.Plugin.Downloader

纯 C# 的 HLS/DASH/MSS 下载器插件（N_m3u8DL-RE 兼容），含抖音下载子模块。

| 项 | 值 |
|---|---|
| PluginId | `B2C3D4E5-F6A7-8901-BCDE-DOWNLOADER001` |
| PluginName | Downloader Plugin |
| PluginAuthor | Downloader |
| PluginVersion | `1.0.0-preview.1` |
| MinPluginSdkVersion | `2.0.0` |
| 支持平台 | **Windows + Linux**（macOS 未覆盖） |
| 类型 | 原生 Avalonia 插件（含 5 个主 Tab + 2 个 Dialog） |

## 功能

### VOD 点播下载
- 解析 HLS（m3u8）/ DASH（mpd）/ MSS（ism）
- 自动选最高码率
- 加密流解密（MP4 原生 AES-128、Shaka Packager）
- 合并转封装（mp4decrypt + mkvmerge）
- 任务队列、并发控制、速率限制、字幕轨道选择
- 任务历史（SQLite 持久化）

### 直播录制
- LiveRecord 引擎
- 多会话管理（LiveSessionRegistry）
- 自定义录制时长/分段

### 抖音子模块（合并自原 `LYBox.Plugin.DouyinDownloader`）
- 首页聚合页（Submit / Jobs / History / Login / Settings 4 Tab + 2 Dialog）
- 提交下载（视频/图集解析、签名、Cookie + msToken 管理）
- 任务管理（队列、状态、重试、并发限速）
- 历史记录（SQLite）
- 登录（msToken / Cookie 持久化到 Data 目录）
- 工具设置（ffmpeg / mp4decrypt / mkvmerge / ShakaPackager 路径、代理、日志级别）

### 解密与转封装工具
- `DecryptMuxPage` —— 单独解密 + 合并页面
- `BinaryLocator` 自动解析用户配置的二进制工具路径

## 设置项

插件在 `RegisterAsync` 中通过 `ISettingsService` 注册设置（分组 `Downloader`）：

| Key | 类型 | 说明 |
|---|---|---|
| `DL.FfmpegPath` | Path | ffmpeg 可执行路径 |
| `DL.Mp4DecryptPath` | Path | mp4decrypt 可执行路径 |
| `DL.MkvmergePath` | Path | mkvmerge 可执行路径 |
| `DL.ShakaPackagerPath` | Path | Shaka Packager 路径 |
| `DL.Proxy` | Text | HTTP 代理 |
| `DL.UseSystemProxy` | Switch | 是否使用系统代理 |
| `DL.LogLevel` | Text | 日志级别（默认 `INFO`） |

## 主要类型

| 类型 | 命名空间 | 作用 |
|---|---|---|
| `DownloaderPlugin` | `LYBox.Plugin.Downloader` | 插件入口；DI 注册 + 设置注册 + 旧数据迁移 |
| `DouyinHomeViewModel` | `LYBox.Plugin.Downloader.ViewModels` | 首页聚合 VM |
| `SubmitViewModel` | 同上 | 提交页 VM |
| `JobsViewModel` | 同上 | 任务页 VM |
| `HistoryViewModel` | 同上 | 历史页 VM |
| `LoginViewModel` | 同上 | 登录页 VM |
| `SettingsViewModel` | 同上 | 设置页 VM |
| `LiveRecordViewModel` | 同上 | 直播录制页 VM |
| `DecryptMuxViewModel` | 同上 | 解密/合并页 VM |
| `VodDownloadViewModel` | 同上 | VOD 下载页 VM |
| `DownloadCoordinator` | `LYBox.Plugin.Downloader.Services` | VOD 下载编排 |
| `LiveRecordEngine` | 同上 | 直播录制引擎 |
| `DouyinDownloadOrchestrator` | `LYBox.Plugin.Downloader.Core` | 抖音下载编排 |
| `DouyinApiClient` | 同上 | 抖音 API 客户端 |
| `SignatureClient` | 同上 | X-Bogus / a_bogus 签名 |
| `PluginDataMigration` | `LYBox.Plugin.Downloader.Config` | 旧版本数据迁移 |
| `WebConsoleServer` | `LYBox.Plugin.Downloader.WebServer` | 内嵌 Web 控制台服务器 |

## 关键依赖

| 包 | 版本 |
|---|---|
| `CliWrap` | `3.10.4` |
| `CommunityToolkit.Mvvm` | `8.4.2` |
| `Microsoft.Data.Sqlite` | `8.0.10` |
| `LYBox.Plugin.Generators` | `$(PluginSdkVersion)` |
| `LYBox.Plugin.Shared` | `$(PluginSdkVersion)` |

## 数据存储

通过 `IPluginDataDirectoryProvider` 解析：

- `%LOCALAPPDATA%/LYBox/{PluginId}/Downloads/` —— VOD/Live 下载产物
- `%LOCALAPPDATA%/LYBox/{PluginId}/downloads.db` —— 任务历史
- `%LOCALAPPDATA%/LYBox/{PluginId}/cookies/` —— 抖音 Cookie + msToken

旧版本（`%APPDATA%/LYBox.Plugin.Downloader/`）由 `PluginDataMigration.MigrateLegacyDouyinData()` 在 `RegisterAsync` 早期一次性迁移。

## 调试

VS Code：`Debug Plugin - Downloader`

## 平台约束

`<PluginSupportedPlatforms>windows,linux</PluginSupportedPlatforms>` —— 抖音/B 站签名路径在 macOS 上未覆盖，宿主据此跳过加载。VOD 解析与下载本身跨平台，macOS 上可考虑手动调整 `<PluginSupportedPlatforms>` 启用。

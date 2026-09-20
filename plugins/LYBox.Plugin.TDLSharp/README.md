# LYBox.Plugin.TDLSharp

[Telegram TDLib](https://github.com/tdlib/td) 集成插件：批量转发、消息导出、媒体下载等。

| 项 | 值 |
|---|---|
| PluginId | `A1B2C3D4-E5F6-7890-ABCD-TDLSHARP00001` |
| PluginName | TDLSharp Plugin |
| PluginAuthor | TDLSharp |
| PluginVersion | `1.0.0-preview.1` |
| MinPluginSdkVersion | `2.0.0` |
| 类型 | 原生 Avalonia 插件（含 TDLib 原生客户端） |

## 功能

- Telegram 账号登录 + Session 持久化
- 消息列表导出（按 Chat / 时间范围 / 媒体过滤）
- 媒体下载（图片、视频、文件）
- 批量转发（按规则：来源 Chat → 目标 Chat）
- 转发去重（SQLite 本地 DB）
- 频道 / 群组 / 用户搜索
- 历史记录持久化

## 设置项

插件在 `RegisterAsync` 中通过 `ISettingsService` 注册设置（分组 `TDL`）：

| Key | 类型 | 说明 | 默认值 |
|---|---|---|---|
| `TDL.TdlRootPath` | Path (folder) | TDLib 数据目录 | `Data/{PluginId}/tdl/` |
| `TDL.ApiId` | Text | Telegram API ID | `tdl_api_id` 环境变量 |
| `TDL.ApiHash` | Text | Telegram API Hash | `tdl_api_hash` 环境变量 |
| `TDL.ProxyServer` | Text | 代理服务器 | `127.0.0.1` |
| `TDL.ProxyPort` | Text | 代理端口 | `7897` |
| `TDL.EnableProxy` | Switch | 启用代理 | `true` |

设置缺失时回退到对应 `tdl_*` 用户环境变量，最后回退到硬编码默认值。

## 主要类型

| 类型 | 命名空间 | 作用 |
|---|---|---|
| `TDLSharpPlugin` | `LYBox.Plugin.TDLSharp` | 插件入口；DI 注册 + 设置注册 + `ShutdownAsync` 释放 TdLib 原生客户端 |
| `TdlClientManager` | `LYBox.Plugin.TDLSharp.Services` | TdLib 客户端管理器（实现 `IDisposable`） |
| `TdlService` | `LYBox.Plugin.TDLSharp.Services` | TDLib 操作封装 |
| `ForwardDb` | 同上 | 转发去重 SQLite |
| `TdlPaths` | 同上 | TDLib 路径解析 |
| `AuthStateCode` | 同上 | 登录认证状态 |
| `ListChatsPage` / `DeepCopyPage` / `DownloadPage` | `LYBox.Plugin.TDLSharp.Pages` | 三个主功能页面 |
| `ScriptParameter` | `LYBox.Plugin.TDLSharp.Models` | 脚本参数模型 |
| `LoginDialog` | `LYBox.Plugin.TDLSharp.Controls` | 登录对话框 |

## 关键依赖

| 包 | 版本 |
|---|---|
| `TDLib` | `1.8.*` |
| `TDLib.Api` | `1.8.*` |
| `tdlib.native` | `1.8.*` |
| `linq2db` | `5.4.1` |
| `Microsoft.Data.Sqlite` | `10.0.9` |
| `LYBox.Plugin.Generators` | `$(PluginSdkVersion)` |
| `LYBox.Plugin.Shared` | `$(PluginSdkVersion)` |

## 数据存储

通过 `IPluginDataDirectoryProvider` 解析（位于 `LYBox.DataRoot/{PluginId}/`）：

- `tdl/` —— TDLib 数据根（默认 `TDL.TdlRootPath`）
- `history/history-{scriptId}.db` —— 历史记录
- `data/forward-{chatId}.db` —— 转发去重

## 原生资源生命周期

TdLib 客户端是原生资源，**必须**在 `ShutdownAsync` 中释放：

```csharp
public Task ShutdownAsync()
{
    if (ServiceLocator.TryGetService<TdlClientManager>(out var manager))
        manager.Dispose();
    return Task.CompletedTask;
}
```

宿主 `App.OnShutdownRequested` 反向调用所有插件的 `ShutdownAsync`，确保 TdLib 连接优雅关闭。

## 调试

VS Code：`Debug Plugin - TDLSharp`

## 前置

- 用户需自行申请 Telegram `api_id` / `api_hash`（[my.telegram.org](https://my.telegram.org)）
- 配置到 `ISettingsService` 或 `tdl_api_id` / `tdl_api_hash` 用户环境变量

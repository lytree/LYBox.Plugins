# LYBox.Plugin.BTSou

BTSOU 资源搜索插件（精简版）。

| 项 | 值 |
|---|---|
| PluginId | `BTSOU-PLUGIN-2026-0000-000000000001` |
| PluginName | BTSou Search |
| PluginAuthor | KiongChan |
| PluginVersion | `1.1.0-preview.1` |
| MinPluginSdkVersion | `2.0.0` |
| 支持平台 | **仅 Windows**（依赖迅雷 COM 互操作） |
| 类型 | 原生 Avalonia 插件 |

## 功能

- BT 资源搜索（基于 BTSOU 资源池）
- 迅雷一键下载（调用本机迅雷客户端）
- 已移除：数据库授权锁、举报系统（原程序保留用于学习研究）

## 关键依赖

- `Interop.ThunderAgentLib.dll` —— 迅雷 COM 互操作（打包在 `lib/` 下，本机需安装迅雷）
- `LYBox.Plugin.Shared`

## 主要类型

| 类型 | 命名空间 | 作用 |
|---|---|---|
| `BTSouPlugin` | `LYBox.Plugin.BTSou` | 插件入口，注册 `BTSouSearchService` 为 DI 单例 |
| `BTSouSearchService` | `LYBox.Plugin.BTSou.Services` | 资源池加载、搜索、链接规范化、迅雷下载 |
| `SearchViewModel` | `LYBox.Plugin.BTSou.ViewModels` | 搜索页面 VM |
| `BTSouConfig` | `LYBox.Plugin.BTSou.Models` | 资源池 URL 等配置 |

## 调试

VS Code：`Debug Plugin - BTSou`

```powershell
$env:AVALONIA_EXTRA_PLUGINS_PATH = (Resolve-Path "artifacts\bin\LYBox.Plugin.BTSou\Debug\net10.0").Path
& "..\LYBox\artifacts\bin\LYBox.Launcher.Desktop\Debug\LYBox.Launcher.Desktop.exe"
```

## 平台约束

`<PluginSupportedPlatforms>windows</PluginSupportedPlatforms>` —— 宿主在非 Windows 平台跳过加载（迅雷 COM 互操作不可用）。

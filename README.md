# LYBox.Plugins

LYBox 桌面应用模板的 **插件仓库**（独立于 [宿主/主题+SDK 仓库](https://github.com/lytree/LYBox)）。

每个插件是 `net10.0` 类库，通过 `LYBox.Plugin.Generators`（Roslyn 源生成器）驱动元数据，引用 `LYBox.Plugin.Shared`（/ `LYBox.Plugin.Shared.Web`）SDK 包。构建产出每个插件一个 zip（含 `plugin.json` 清单），供宿主在 `plugins/` 与 `AVALONIA_EXTRA_PLUGINS_PATH` 下加载。

> 插件业务版本由各插件 csproj 内 `<PluginVersion>` 独立管理，不受 SDK 版本控制。

---

## 目录

- [快速开始](#快速开始)
- [构建与运行](#构建与运行)
- [SDK 依赖解析](#sdk-依赖解析)
- [产物与清理](#产物与清理)
- [插件调试](#插件调试)
- [创建新插件](#创建新插件)
- [脚手架模板](#脚手架模板)
- [版本真相源](#版本真相源)

---

## 快速开始

```powershell
# 1. 在 LYBox 仓库构建宿主 + SDK NuGet 包
cd ..\LYBox
.\build.ps1 --build=bin

# 2. 回到本仓库，构建所有插件
cd ..\LYBox.Plugins
.\build.ps1

# 3. 调试插件（VS Code：F5 → "Debug Plugin - {Name}"）
```

Linux/macOS 用 `./build.sh` 替代 `.\build.ps1`。

---

## 构建与运行

**Cake.Sdk 文件化应用**（`build/build.cs`，Cake.Sdk **6.2.0**）。通过 `.\build.ps1`（Windows）或 `./build.sh`（Linux/macOS）调用。

```powershell
# 构建并打包所有插件为 zip
.\build.ps1 --build=plugin --configuration=Release --sdk-feed=nuget

# 从本地 SDK feed 构建
.\build.ps1 --build=plugin --sdk-feed=local

# 显式指定本地 feed 目录
.\build.ps1 --build=plugin --sdk-feed=local --sdk-feed-path=<dir>

# 覆盖 SDK 包版本
.\build.ps1 --build=plugin --sdk-version=2.3.0

# 仅构建指定插件（逗号分隔多个）
.\build.ps1 --build=plugin --plugin=LYBox.Plugin.Template
.\build.ps1 --build=plugin --plugin=LYBox.Plugin.Template,LYBox.Plugin.WebTemplate

# 覆盖所有插件版本
.\build.ps1 --plugin-version=1.2.3

# 覆盖配置（默认 Release）
.\build.ps1 --configuration=Debug
```

---

## SDK 依赖解析

插件通过 `PackageReference` 引用：

| 包 | 作用 |
|---|---|
| `LYBox.Plugin.Generators` | Roslyn 源生成器（analyzer） |
| `LYBox.Plugin.Shared` | 核心契约（必需） |
| `LYBox.Plugin.Shared.Web` | Web 插件（仅 Web） |
| `LYBox.Plugin.CommandLine` | CLI 插件（仅 CLI） |

版本用 `$(PluginSdkVersion)`（默认取仓库根 `version.props` 的 `<LyboxVersion>`）。

SDK 来源由 `--sdk-feed` 控制：

| 值 | 说明 |
|---|---|
| `local` | 从本地 feed 解析：`--sdk-feed-path` > `env:LYBOX_SDK_FEED` > 默认 `artifacts/packages/sdk`。适用 SDK 刚发、尚未上 nuget.org 时 |
| `nuget` | 从 nuget.org 解析（需 SDK 已发布） |

未显式传 `--sdk-feed` 时自动判断：设了 `LYBOX_SDK_FEED` 或 `artifacts/packages/sdk` 下已有 `.nupkg` 则用本地，否则用 nuget。

**本地 feed 示例**：

```powershell
$env:LYBOX_SDK_FEED = "F:\Code\Dotnet\AvaloniaTemplate\artifacts\packages\sdk"
.\build.ps1 --build=plugin --sdk-feed=local
```

`plugins/` 下各插件的 SDK 版本由 `version.props` 的 `<LyboxVersion>` 统一声明，发版随宿主同步对齐。

---

## 产物与清理

### 产物

- 所有项目的 `bin/`、`obj/` 统一进入 `artifacts/bin/` 与 `artifacts/obj/`（由 `Directory.Build.props` 驱动）。
- 插件 zip 包（含 `plugin.json`，剥离 `.pdb` / `.xml` / `.deps.json` / `.runtimeconfig.json`）—— **唯一长期保留的产物**：`artifacts/packages/plugins/{Name}-{Version}.zip`。

### Clean

`build.ps1 --target=Clean`（或隐式触发：`--build=plugin` 会先跑 `Clean`）会删除：

| 删除项 | 说明 |
|---|---|
| `artifacts/bin/` | 所有项目的构建输出 |
| `artifacts/obj/` | 所有项目的中间编译产物 |
| `artifacts/publish/` | 插件 publish 中间产物 |
| `artifacts/packages/plugins/*.zip` | 已存在的旧版本 zip |

> Clean 在脚本自身运行时会被 Cake.Sdk 文件锁阻塞一次，`CleanDirectoryIfExists` 重试 4 次后跳过并打印告警，不会中断流程。下次独立运行 Clean 会清理干净。

---

## 插件调试

开发期调试使用 `artifacts/bin/{Name}/debug/`（由 `dotnet build` 自动生成）。VS Code 调试配置 `Debug Plugin - {Name}` 通过 `AVALONIA_EXTRA_PLUGINS_PATH` 指向该目录。

> **调试插件不会自动构建宿主**：`build-plugin: {Name}` 任务的 `dependsOn` 已设为空数组，避免每次调试插件都重复编译宿主。宿主需手动构建一次：先跑 `build-host-debug` 任务或在该仓库直接 `dotnet build LYBox.Launcher.Desktop`。

### 已配置的调试插件

| 插件 | 调试入口 |
|------|---------|
| Buttons & Inputs | `Debug Plugin - Buttons & Inputs` |
| DateTime | `Debug Plugin - DateTime` |
| DialogFeedbacks | `Debug Plugin - DialogFeedbacks` |
| Downloader | `Debug Plugin - Downloader` |
| LayoutDisplay | `Debug Plugin - LayoutDisplay` |
| NavigationMenus | `Debug Plugin - NavigationMenus` |
| ProDataGrid | `Debug Plugin - ProDataGrid` |
| ScottPlot | `Debug Plugin - ScottPlot` |
| TDLSharp | `Debug Plugin - TDLSharp` |
| BTSou | `Debug Plugin - BTSou` |
| ViteSample（Vite dev） | `Debug Plugin - ViteSample (Vite dev + C# break)` |
| ViteSample（WebView prod） | `Debug Plugin - ViteSample (WebView prod)` |
| WebTemplate | `Debug Plugin - WebTemplate` |

同时提供 `Debug All Plugins` compound（`preLaunchTask=build-all-plugins`，`stopAll=true`）一次性启动全部插件调试。`Attach to LYBox.Launcher.Desktop` 用于附加到已运行的宿主进程。

---

## 创建新插件

复制 [`templates/plugin-template-aot`](templates/plugin-template-aot)（原生 Avalonia）或 [`templates/web-plugin-vanilla`](templates/web-plugin-vanilla)（vanilla Web）目录，修改命名空间与 PluginId。详细步骤见各模板的 README。

最小原生插件 csproj：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Library</OutputType>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>

    <PluginId>YOUR-UUID</PluginId>
    <PluginName>My Plugin</PluginName>
    <PluginAuthor>Author</PluginAuthor>
    <PluginDescription>Description</PluginDescription>
    <PluginVersion>1.0.0</PluginVersion>
    <MinPluginSdkVersion>2.3.0-preview.3</MinPluginSdkVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="LYBox.Plugin.Generators" Version="$(PluginSdkVersion)"
                      OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
    <PackageReference Include="LYBox.Plugin.Shared" Version="$(PluginSdkVersion)" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

完整开发规范（特性、UI、SDK 版本等）见 [LYBox README](../LYBox/README.md) 与 [LYBox/docs/](../LYBox/docs/)。

---

## 脚手架模板

| 模板 | 适用场景 |
|---|---|
| [`templates/plugin-template-aot`](templates/plugin-template-aot) | 原生 Avalonia 插件（含 CLI 注册示例、本地化、图标） |
| [`templates/web-plugin-vanilla`](templates/web-plugin-vanilla) | vanilla HTML/JS Web 插件（零依赖、最短路径） |

完整端到端示例：[`plugins/LYBox.Plugin.ViteSample`](plugins/LYBox.Plugin.ViteSample) —— Vite + TypeScript 双形态联调。

> 原 `LYBox.Plugin.Template` 与 `LYBox.Plugin.WebTemplate` 的可复制模板已迁移到 `templates/`；这些目录**不参与** `build.cs` 打包，也不进 `Plugins.slnx`，开发者复制为自己的插件后即可使用。

---

## 版本真相源

文件 `version.props`：

- `<LyboxVersion>` = Plugin SDK 契约版本（默认对齐宿主版本）
- 各插件 `<PluginVersion>` 在各自 `csproj` 独立声明

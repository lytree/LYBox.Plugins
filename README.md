# LYBox.Plugins

LYBox 桌面应用模板的**插件仓库**（独立于 [宿主/主题+SDK 仓库](https://github.com/lytree/LY.Tool)）。

每个插件是 `net10.0` 类库，通过 `LYBox.Plugin.Generators`（Roslyn 源生成器）驱动元数据，
引用 `LYBox.Plugin.Shared`（/ `LYBox.Plugin.Shared.Web`）SDK 包。构建产出每个插件一个 zip（含 `plugin.json` 清单），
供宿主在 `plugins/` 与 `AVALONIA_EXTRA_PLUGINS_PATH` 下加载。

> 插件业务版本由各插件 csproj 内 `<PluginVersion>` 独立管理，不受 SDK 版本控制。

---

## 构建与运行

- **构建系统**：Cake.Sdk（`build/build.cs` — .NET 10 文件化应用，Cake.Sdk 6.2.0）。

  ```
  .\build.ps1 --build=plugin --configuration=Release --sdk-feed=nuget   # 默认：从 nuget.org 解析 SDK，构建并打包所有插件
  .\build.ps1 --build=plugin --sdk-feed=local                           # 从本地 SDK feed 构建
  .\build.ps1 --build=plugin --sdk-feed=local --sdk-feed-path=<dir>     # 显式指定本地 feed 目录
  .\build.ps1 --build=plugin --sdk-version=2.3.0                        # 覆盖 SDK 包版本（注入 PluginSdkVersion）
  .\build.ps1 --build=plugin --plugin=LYBox.Plugin.Template             # 仅构建指定插件（逗号分隔多个）
  .\build.ps1 --configuration=Debug                                     # 覆盖配置（默认：Release）
  ```

  Linux/macOS 用 `./build.sh` 替代 `.\build.ps1`。

## SDK（Plugin SDK）依赖解析

插件通过 `PackageReference` 引用 `LYBox.Plugin.Generators`、`LYBox.Plugin.Shared`、`LYBox.Plugin.Shared.Web`、
`LYBox.Plugin.CommandLine`，版本使用 `$(PluginSdkVersion)`（默认取仓库根 `version.props` 的 `<LyboxVersion>`）。

SDK 来源支持两种模式（`--sdk-feed`）：

| 值      | 说明                                                                                     |
| ------- | ---------------------------------------------------------------------------------------- |
| `local` | 从本地 feed 解析：`--sdk-feed-path` > `env:LYBOX_SDK_FEED` > 默认 `artifacts/packages/sdk`。适用 SDK 刚发、尚未上 nuget.org 时。 |
| `nuget` | 从 nuget.org 解析（需 SDK 已发布）。                                                      |

未显式传 `--sdk-feed` 时自动判断：设了 `LYBOX_SDK_FEED` 或 `artifacts/packages/sdk` 下已有 `.nupkg` 则用本地，否则用 nuget。

本地 feed 路径示例（PowerShell）：

  ```powershell
  $env:LYBOX_SDK_FEED = "F:\Code\Dotnet\AvaloniaTemplate\artifacts\packages\sdk"
  .\build.ps1 --build=plugin --sdk-feed=local
  ```

`plugins/` 下各插件的 SDK 版本由 `version.props` 的 `<LyboxVersion>` 统一声明，发版随宿主同步对齐。

## 产物

- 插件构建中间产出：`artifacts/publish/plugins/{Name}/publish/`
- 插件 zip 包（含 `plugin.json`，剥离 `.pdb` / `.xml` / `.deps.json` / `.runtimeconfig.json`）：`artifacts/packages/plugins/{Name}-{Version}.zip`

## 版本真相源

文件 `version.props`：
- `<LyboxVersion>` = Plugin SDK 契约版本（默认对齐宿主版本）
- 各插件 `<PluginVersion>` 在各自 `csproj` 独立声明

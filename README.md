# LYBox.Plugins

LYBox 桌面应用模板的**插件仓库**（独立于 [宿主/主题+SDK 仓库](https://github.com/lytree/LYBox)）。

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

- 所有项目的 `bin/`、`obj/` 统一进入根目录 `artifacts/bin/` 与 `artifacts/obj/`（由 `Directory.Build.props` 的 `UseArtifactsOutput=true` + `ArtifactsPath=artifacts` 驱动，仓库根与各插件子目录不再生成 `bin`/`obj`）。
- 插件 zip 包（含 `plugin.json`，剥离 `.pdb` / `.xml` / `.deps.json` / `.runtimeconfig.json`）—— **唯一长期保留的产物**：`artifacts/packages/plugins/{Name}-{Version}.zip`。

## 清理

`build.ps1 --target=Clean`（或隐式触发：`--build=plugin`、`--build=plugin --plugin=...` 都会先跑 `Clean`）会执行以下删除：

| 删除项                                          | 说明                                                       |
| ---------------------------------------------- | ---------------------------------------------------------- |
| `artifacts/bin/`                               | 所有项目的构建输出（dll / pdb / deps.json 等）                  |
| `artifacts/obj/`                               | 所有项目的中间编译产物（GeneratedFiles、ref、refint、Up2Date 等） |
| `artifacts/publish/`                           | 插件 publish 中间产物（被 zip 打包消费后即丢弃）                  |
| `artifacts/packages/plugins/*.zip`             | 已存在的旧版本 zip（在重新打包前清空，避免残留）                  |

`Clean` 之后 `artifacts/` 下只保留 `packages/` 目录框架（目录本身不删，便于后续步骤 `EnsureDirectoryExists`）。

**开发期调试**请使用 `artifacts/bin/{Name}/debug/`：

- 该目录由 `dotnet build`（VS Code 任务 `build-plugin: {Name}`、`build-all-plugins`）自动生成；
- VS Code 调试配置 `Debug Plugin - {Name}` 通过 `AVALONIA_EXTRA_PLUGINS_PATH=${workspaceFolder}/artifacts/bin/{Name}/Debug` 加载；
- `Clean` 后首次调试会自动重建（先跑 `build-plugin` preLaunchTask，再启动宿主 Launcher）。

**注意**：`Clean` 在脚本自身运行时会被 Cake.Sdk 持有的 `artifacts/bin/debug/Cake.*.dll` 文件锁阻塞一次，此时 `CleanDirectoryIfExists` 会重试 4 次后跳过并打印告警（已有文件锁防御），不会中断流程。下次独立运行 `Clean` 时会清理干净。

## 版本真相源

文件 `version.props`：
- `<LyboxVersion>` = Plugin SDK 契约版本（默认对齐宿主版本）
- 各插件 `<PluginVersion>` 在各自 `csproj` 独立声明

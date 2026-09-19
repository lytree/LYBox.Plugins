# Plugin Template — 原生 Avalonia 插件模板

模板来源：`plugins/LYBox.Plugin.Template`（已于本仓库迁移）。

## 适用场景

- 想写一个 **纯 Avalonia UI + 可选 CLI 子命令** 的插件（不涉及 WebView / 前端页面）。
- 需要一个最小可运行基线：入口类、菜单/导航注册、CLI 注册、本地化、图标。

## 使用步骤

1. 把本目录**整目录复制**到 `LYBox.Plugins/plugins/` 下，例如 `LYBox.Plugin.MyFeature/`。
2. 重命名：
   - `LYBox.Plugin.Template.csproj` → `LYBox.Plugin.MyFeature.csproj`
   - 文件内 `TemplatePlugin` 类 → `MyFeaturePlugin`
   - `LYBox.Plugin.Template` 命名空间 → `LYBox.Plugin.MyFeature`
   - `TemplateCliRegistrar.CommandName` → `myfeature`
3. 修改 csproj：
   - `<PluginId>` 改为你的全局唯一 GUID
   - `<PluginName>` / `<PluginAuthor>` / `<PluginDescription>` / `<PluginVersion>` 改为实际值
4. 替换 `Pages/TemplatePage.axaml` 与 `ViewModels/TemplatePageViewModel.cs` 为你的页面。
5. 在 `ViewModels/` 上保留 `[NavigationItem("...")]` `[Menu("...", "...", ...)]` `[ViewMap(...)]` 三个特性 —— 宿主会自动发现并注册。
6. 如不需要 CLI，删除 `TemplateCliRegistrar.cs` 与 csproj 中 3 个 `PluginCli*` 属性。

## 包含的演示能力

- `[GenerateMetadata]` 入口类 —— 源生成器自动实现 `IPlugin` + `IPluginMetadata`
- `[NavigationItem]` / `[Menu]` / `[ViewMap]` —— UI 注册三件套
- `[ObservableProperty]` + `[RelayCommand]` —— CommunityToolkit.Mvvm 模式
- `TemplateCliRegistrar` —— CLI 子命令注册（`myfeature hello --name=xxx`）
- `Resources/Strings.resx` + `Strings.zh-CN.resx` —— 中英双语本地化
- `PluginIcons.axaml` —— 插件菜单图标资源

## 调试

把 `LYBox.Plugins/plugins/LYBox.Plugin.MyFeature/Properties/launchSettings.json` 的 `AVALONIA_EXTRA_PLUGINS_PATH` 已配置为指向 `$(TargetDir)`，VS Code 启动会拉起宿主 `LYBox.Launcher.Desktop.exe` 并加载插件 DLL。

## 不要修改

- `LYBox.Plugin.Template.csproj` 中的 `IsPluginProject=true` —— 宿主依据此识别插件类型。
- `Resources/Strings.Designer.cs` 中的 `ResourceManager` 完整命名空间 —— 与 `Strings.resx` 的 logical name 一致。
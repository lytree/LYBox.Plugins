# LYBox.Plugin.ViteSample

端到端示例：**Vite + TypeScript 前端 + 强类型 RPC + 双形态联调**。

- **后端**：`Rpc/SampleCommands.cs` 定义 `[RpcCommand]` 方法（GreetAsync / AddAsync / GetSampleInfoAsync），源生成器自动产出 `wwwroot/.lybox/SampleCommands.client.{js,d.ts}`。
- **Avalonia 页面**：`Pages/ViteSamplePage.axaml` 使用 `<web:WebPluginView>` 承载 WebView，每 2 秒经 SSE 推一次 `tick` 事件。
- **前端**：
  - `wwwroot/index.html`：内置 vanilla 兜底页（无 Vite 也能跑通）
  - `ui/`：Vite + TypeScript 工程，IDE 从生成器 .d.ts 拿完整强类型提示
  - dev 经 Vite 同源代理访问宿主 RPC/SSE；prod dist 拷入 wwwroot 后由宿主 `/sdk/lybox-plugin-sdk.js` 接管

@version 2.3.0-preview.7

## 快速开始（Vite dev）

```powershell
# 终端 A：宿主（自动拉 Vite —— 如果用 LYBox.Plugins/.vscode/launch.json "Debug Plugin - ViteSample" 则自动）
cd F:\Code\Dotnet\LYBox\LYBox.Plugins
dotnet build plugins\LYBox.Plugin.ViteSample\LYBox.Plugin.ViteSample.csproj -c Debug

# 启动宿主（VSCode F5 或命令行）
$env:AVALONIA_EXTRA_PLUGINS_PATH="F:\Code\Dotnet\LYBox\LYBox.Plugins\artifacts\bin\LYBox.Plugin.ViteSample\Debug"
$env:LYBOX_WEB_PORT="58080"
$env:LYBOX_VITE_DIR="F:\Code\Dotnet\LYBox\LYBox.Plugins\plugins\LYBox.Plugin.ViteSample\ui"
& "F:\Code\Dotnet\LYBox\LYBox\artifacts\bin\LYBox.Launcher.Desktop\Debug\LYBox.Launcher.Desktop.exe"
```

启动器 `--web-vite` 自动探测空闲端口写入 `LYBOX_WEB_PORT`，拉起 `npm run dev`。  
浏览器打开 `http://localhost:5173` 享受完整 HMR + 同源代理联调宿主。

## 调试 C# 端

在 VS Code 的 `LYBox.Plugins/.vscode/launch.json` 中新增（或复用模板）：

```jsonc
{
  "name": "Debug Plugin - ViteSample",
  "type": "coreclr",
  "request": "launch",
  "preLaunchTask": "build-plugin: ViteSample",
  "program": "${workspaceFolder}/../LYBox/artifacts/bin/LYBox.Launcher.Desktop/Debug/LYBox.Launcher.Desktop.dll",
  "env": {
    "AVALONIA_EXTRA_PLUGINS_PATH": "${workspaceFolder}/artifacts/bin/LYBox.Plugin.ViteSample/Debug",
    "LYBOX_WEB_PORT": "58080",
    "LYBOX_VITE_DIR": "${workspaceFolder}/plugins/LYBox.Plugin.ViteSample/ui"
  }
}
```

可断点位置：
- `Rpc/SampleCommands.cs` 中 `GreetAsync` / `AddAsync` / `GetSampleInfoAsync`（前端调用即命中）
- `Pages/ViteSamplePage.axaml.cs` 中 `OnPushTick`（SSE 推送入口）
- `LYBox.Plugin.Shared.Web/Web/WebHostService.cs` 中 `HandleRpc`（HTTP 桥）

## 生产打包

```powershell
cd F:\Code\Dotnet\LYBox\LYBox.Plugins
.\build.ps1 --plugin=LYBox.Plugin.ViteSample
# 产物: artifacts/packages/plugins/LYBox.Plugin.ViteSample-1.0.0-preview.1.zip
#       含 wwwroot/ + plugin.json + .dll
```

注：当前 `build.cs` 仅复制 `wwwroot/` 内的 vanilla 兜底页；如需把 `ui/dist/` 注入，
可在 build.cs 的 `PackPlugins` 任务前加一步 `npm run build` + 拷贝 `ui/dist/* → wwwroot/`。

## 目录结构

```
LYBox.Plugin.ViteSample/
├── LYBox.Plugin.ViteSample.csproj   # 后端：net10.0 + Web 插件声明
├── Rpc/
│   └── SampleCommands.cs            # [RpcCommand] 源生成器入口
├── Pages/
│   ├── ViteSamplePage.axaml         # <web:WebPluginView> 承载
│   └── ViteSamplePage.axaml.cs      # SSE 推送 Timer
├── ViewModels/
│   └── ViteSamplePageViewModel.cs   # [NavigationItem] [Menu] [ViewMap]
├── wwwroot/                         # 宿主静态服务根目录
│   ├── index.html                   # vanilla 兜底页（生产可被 ui/dist 覆盖）
│   └── .lybox/
│       └── mock.json                # lybox-mock 浏览器脱机演示数据
├── ui/                              # Vite 工程（独立 package.json / 依赖）
│   ├── package.json
│   ├── tsconfig.json
│   ├── vite.config.ts               # 同源代理 + dev/prod alias 切换
│   ├── index.html                   # Vite 入口（含 /sdk/lybox-plugin-sdk.js）
│   └── src/
│       ├── lybox.ts                 # 重导出层（dev/prod 切换由 vite alias 完成）
│       ├── lybox.dev.ts             # Vite dev 形态：完整 HTTP 桥接 + SSE
│       ├── lybox.prod.ts            # 生产形态：透传宿主 window.LyboxPlugin
│       ├── main.ts                  # 业务入口
│       └── api/
│           └── samples.ts           # 强类型客户端（消费生成器 .d.ts）
├── Properties/
│   └── launchSettings.json          # VS Code 调试配置（Vite dev / WebView prod 双形态）
├── PluginIcons.axaml                # 插件图标
└── README.md
```

## 与 templates/ 的关系

- `LYBox.Plugins/templates/plugin-template-aot/`：原生 Avalonia 插件模板（不含 Vite）。
- `LYBox.Plugins/templates/web-plugin-vanilla/`：vanilla HTML/JS Web 插件模板（无 Vite）。
- **本插件**：Vite + TypeScript 端到端示例（完整工程，可直接 F5 联调）。

## 故障排查

| 现象 | 原因 | 解决 |
|------|------|------|
| `npm run dev` 后 5173 无法访问宿主 58080 | `LYBOX_WEB_PORT` 未在两端一致 | PowerShell 设 `$env:LYBOX_WEB_PORT="58080"` 后再启动宿主 |
| RPC 报 "调试会话签发失败" | 宿主为 Release 构建 | `--configuration=Debug` 启动宿主；Release 无 `/__lybox/debug/session/*` 端点 |
| SSE 持续重连 | Token 过期或 pluginId 不匹配 | 清 `sessionStorage` 中 `lybox-dev-session:*` 后刷新 |
| WebView 报 "插件未主动注册 Web 资源" | csproj 缺 `<PluginKind>Web</PluginKind>` 或入口类未实现 `IWebPlugin` | 检查 csproj 与 `ViteSamplePlugin.cs` |
| IDE 看不到 `samples.greet` 强类型 | 生成器未跑过 | 至少 `dotnet build` 一次插件工程；产物位于 `bin/Debug/.../wwwroot/.lybox/SampleCommands.client.d.ts` |
| `dist/*.js` 含 SDK 字节 | vite alias 未生效 | 检查 `vite build --mode production`；确认 `resolve.alias` 在 production 分支内 |
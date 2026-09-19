# Web Plugin Vanilla Template

模板来源：`plugins/LYBox.Plugin.WebTemplate/wwwroot/`。

## 适用场景

- 想写一个 **WebView + 原生 IPC** 的插件，但前端不引入 Vite / 任何构建工具。
- 想要"零依赖（vanilla HTML/JS）"快速跑通 IPC + SSE 全链路。
- 想要"复制 wwwroot/ 到我自己的插件就能跑"的最短路径。

## 使用步骤

1. 把本目录下的 `wwwroot-scaffold/` 整目录复制到你的插件工程里，重命名为 `wwwroot/`（或保持现状，csproj 中 `<PluginWwwroot>` 改为新目录名）。
2. csproj 必备声明：
   ```xml
   <PropertyGroup>
     <IsPluginProject>true</IsPluginProject>
     <PluginKind>Web</PluginKind>
     <PluginWwwroot>wwwroot</PluginWwwroot>
     <PluginEntryPage>index.html</PluginEntryPage>
   </PropertyGroup>
   <ItemGroup>
     <PackageReference Include="LYBox.Plugin.Shared.Web" Version="$(PluginSdkVersion)" PrivateAssets="all" />
   </ItemGroup>
   ```
3. 入口类实现 `IWebPlugin`：
   ```csharp
   [GenerateMetadata]
   public partial class MyWebPlugin : IWebPlugin { }
   ```
   源生成器从 csproj 的 `<PluginKind>Web</PluginKind>` 自动实现 `Web` 描述符，宿主据此统一注册 wwwroot。

## 内置演示能力

| 文件 | 能力 |
|------|------|
| `index.html` | JS → C# RPC（`greet`、`add`、`getPluginInfo`）+ C# → JS SSE（`tick`）+ 系统 API（OpenFilePicker / ShowMessageBox 等） |
| `.lybox/mock.json` | 浏览器形态下由 lybox-mock CLI 读取的本地假数据（无需宿主即可演示） |
| `import { createLyboxClient, invoke, on, isWebView } from '/sdk/lybox-plugin-sdk.js'` | 宿主嵌入式 SDK（来自 `LYBox.Plugin.Shared.Web/Assets/lybox-plugin-sdk.js`），dev 与生产共用 |

## 约束

- 前端页面**不引用** `node_modules`，不引入 Vite。构建期产物 = `wwwroot/` 内的静态文件。
- IDE 提示来自宿主 `/sdk/lybox-plugin-sdk.d.ts`（宿主进程运行后由 `WebHostService` 在 `/sdk/` 暴露）。
- C# RPC 方法名遵循源生成器规则：去 `Async` 后缀并 camelCase（如 `GreetAsync` → `greet`）；多参数用 DTO record。

## 与 ViteSample 的差异

| 维度 | web-plugin-vanilla（本模板） | LYBox.Plugin.ViteSample |
|------|------------------------------|--------------------------|
| 前端构建工具 | 无（直接 HTML/JS） | Vite + TypeScript（dev 期 HMR） |
| 包大小 | 仅 SDK + index.html | 同 + Vite 编译产物（dev 期不入 dist） |
| 联调方式 | `dotnet run` + 浏览器打开 WebView | `--web-vite` 启动 Vite dev server，HMR + 同源代理 |
| 学习曲线 | 极低（一个 HTML 文件） | 中等（需要 Node.js + Vite） |
| 适用 | 演示、简单交互 | 复杂 UI（React/Vue）+ 多人协作 |
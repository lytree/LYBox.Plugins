# LYBox.Plugin.Zitie

**字帖生成器** Web 插件 —— Vue 3 + TSX + Vite 前端，嵌入 LYBox 桌面宿主的 `<web:WebPluginView>`。

@version 2.4.0-beta.1

对标 [z2h.cn](https://z2h.cn) 的离线复刻版：10 种字帖类型、10 种方格、942 字笔顺路径、描红/拼音/A4 排版，可导出 PDF / PNG。

---

## 快速开始

### 生产（WebView）

```powershell
# 1. 构建前端 + 打包插件（build.cs 会自动 pnpm build 并注入 wwwroot/）
cd F:\Code\Dotnet\LYBox\LYBox.Plugins
.\build.ps1 --plugin=LYBox.Plugin.Zitie

# 产物: artifacts/packages/plugins/LYBox.Plugin.Zitie-1.0.0-preview.1.zip
```

前端产物同步步骤已内建在 `build.cs` 的 `CopyPluginWwwroot`：csproj 声明 `<ZitieUiDir>ui</ZitieUiDir>` 时，打包阶段会自动执行 `pnpm run build` 并把 `ui/dist/*` 覆盖到 `wwwroot/`。手工同步：

```powershell
cd plugins\LYBox.Plugin.Zitie\ui
pnpm install
pnpm build
Copy-Item -Recurse -Force dist\* ..\wwwroot\
```

### 开发（Vite HMR）

VS Code 直接 F5 选：

| 配置 | 说明 |
|------|------|
| `Debug Plugin - Zitie (Vite dev + C# break)` | 宿主拉起 Vite dev server，浏览器开 `http://127.0.0.1:5178` 联调 |
| `Debug Plugin - Zitie (WebView prod)` | 直接加载 `wwwroot/`，验证生产形态 |

命令行等价：

```powershell
$env:AVALONIA_EXTRA_PLUGINS_PATH="F:\Code\Dotnet\LYBox\LYBox.Plugins\artifacts\bin\LYBox.Plugin.Zitie\Debug"
$env:LYBOX_WEB_PORT="58080"
$env:LYBOX_VITE_DIR="F:\Code\Dotnet\LYBox\LYBox.Plugins\plugins\LYBox.Plugin.Zitie\ui"
& "F:\Code\Dotnet\LYBox\LYBox\artifacts\bin\LYBox.Launcher.Desktop\Debug\LYBox.Launcher.Desktop.dll"
```

> 宿主须为 **Debug** 构建：RPC 调试会话端点 `/__lybox/debug/session/*` 在 Release 下不存在。
> 调试宿主不会自动编译，先跑一次 `build-plugin: Zitie` 任务。

---

## 架构

```
LYBox.Plugin.Zitie/
├── LYBox.Plugin.Zitie.csproj      # net10.0，PluginKind=Web，ZitieUiDir=ui
├── ZitiePlugin.cs                 # [GenerateMetadata] IWebPlugin 入口
├── Pages/ZitiePage.axaml(.cs)     # <web:WebPluginView> 承载 + 注册原生对话框上下文
├── ViewModels/                    # [NavigationItem] [Menu] [ViewMap]
├── Rpc/
│   ├── ZitieCommands.cs           # [RpcCommand] saveBytes / readTextFile / openPath
│   └── ZitieNativeHost.cs         # TopLevel → StorageProvider 桥接
├── PluginIcons.axaml              # 田字格图标
├── wwwroot/                       # 前端构建产物（build 时由 ui/dist 覆盖）
└── ui/                            # Vite 工程（源码唯一副本）
    ├── src/
    │   ├── lybox.ts               # 桥接统一出口
    │   ├── lybox.dev.ts           # Vite dev：经同源代理走 HTTP RPC 桥
    │   ├── lybox.prod.ts          # 生产：透传宿主 window.LyboxPlugin
    │   ├── lybox-env.ts           # 宿主全局类型单一真相源
    │   ├── core/native-bridge.ts  # 导出/导入/打印的宿主 RPC 封装
    │   ├── core/exporter.ts       # PNG / PDF / 打印（宿主优先，浏览器降级）
    │   ├── core/txt-import.ts     # TXT 规范化与语言识别（纯逻辑，无 IO）
    │   └── components/            # App / Sidebar / Toolbar / PagePreview ...
    └── public/data/hanzi-data/    # 942 字笔顺 SVG 路径（4.3 MB）
```

### 双形态桥接

`ui/src/lybox.ts` 是唯一出口，实现体按 Vite mode 切换（`vite.config.ts` 的 `resolve.alias`）：

| 形态 | 实现 | RPC 通道 |
|------|------|---------|
| dev（浏览器 HMR） | `lybox.dev.ts` | `POST /__bridge/{pluginId}/rpc` + 调试会话 token |
| prod（宿主 WebView） | `lybox.prod.ts` | `window.LyboxPlugin.invoke`（`/sdk/lybox-plugin-sdk.js`） |

生产构建会把 `./lybox.dev` 重写为 `./lybox.prod`，`dist/*.js` 不含 dev 桥接字节。

### 为什么不用浏览器原生下载

WebView2 / WPE WebKit 内嵌浏览器没有可靠的 blob 落地路径——`<a download>` 常静默失败，`<input type="file">` 的选择器也不受宿主控制。因此：

| 功能 | WebView 内 | 纯浏览器 |
|------|-----------|---------|
| 导出 PDF / PNG | 原生保存对话框 + 宿主写盘 | `a[download]` |
| 导入 TXT | 原生打开对话框 + 宿主读盘（UTF-8 优先 / GBK 回退） | `<input type="file">` |
| 打印 | 生成 PDF → 系统默认程序打开 | `window.print()` |

**打印的取舍**：Avalonia 无跨平台打印对话框（Windows 需 `System.Drawing.Printing`，Linux/macOS 无等价 API）。故采用「生成 PDF → 交给系统默认程序」，由用户在阅读器中打印——行为跨平台一致，代价是多一步。

### RPC 契约

| 方法（C#） | RPC 名（前端） | 载荷 | 返回 |
|-----------|--------------|------|------|
| `SaveBytesAsync` | `saveBytes` | `{ fileName, base64, filterName, extensions }` | `string \| null`（实际路径 / 取消） |
| `ReadTextFileAsync` | `readTextFile` | `{ fileName, filterName, extensions }` | `{ text, fileName } \| null` |
| `OpenPathAsync` | `openPath` | `{ path }` | `boolean` |

方法名规则由源生成器定：去 `Async` 后缀 + camelCase；多参数必须打包为单个 DTO record。

前端类型声明在 `ui/src/core/native-bridge.ts`，与 `Rpc/ZitieCommands.cs` 的 record 一一对应——**改一侧必须同步另一侧**。

> `wwwroot/.lybox/ZitieCommands.client.{js,d.ts}` 由源生成器产出，但**运行时**才经 `RegisterGeneratedClientArtifact` 落盘（宿主跑过一次 Web 插件后才有）。故前端直接用 `invoke()` 并自维护类型，不依赖该产物参与构建。

---

## 命令

| 命令 | 说明 |
|------|------|
| `pnpm dev` | 启动 Vite dev server（127.0.0.1:5178） |
| `pnpm build` | 类型检查 + 生产构建 → `ui/dist/` |
| `pnpm typecheck` | 仅类型检查（strict） |
| `pnpm preview` | 预览构建产物 |
| `pnpm verify` | 渲染完整性检查（需先启动 dev） |
| `pnpm fetch:strokes` | 抓取笔顺数据（`tools/charset.js` 定义汉字表） |

---

## 故障排查

| 现象 | 原因 | 解决 |
|------|------|------|
| WebView 显示「插件未主动注册 Web 资源」 | 缺 `plugin.json` 或 `wwwroot/index.html` | `dotnet build` 后确认 `artifacts/bin/.../wwwroot/index.html` 存在 |
| 页面 404，笔顺全空 | `base` 不是相对路径 | `vite.config.ts` 须 `base: './'`（宿主挂在 `/{pluginId}/` 下） |
| `LYBox SDK 未挂载` | index.html 未注入 SDK，或以经典脚本加载 ESM | 确认 index.html 头部内联脚本已注入 `/sdk/lybox-plugin-sdk.js` 且 `type="module"`；桥接在 SDK 缺失时会自动回退宿主原生桥 `window.__lybox` |
| Vite 构建报 `Failed to resolve /sdk/lybox-plugin-sdk.js` | index.html 里直接写 `<script src="/sdk/...">` / `<link href="/sdk/...">` | 改为运行时内联脚本注入（见 `ui/index.html`），Vite 不解析动态创建的资源 |
| 插件配色不跟随宿主主题 | 未引入 SDK 主题 CSS | index.html 注入 `/sdk/lybox-plugin-theme.css`；业务 CSS 只引用 `--lybox-*` 变量（宿主会在导航完成与主题切换时写入 `data-lybox-theme`） |
| 宿主标题栏关闭按钮点不到 / 被页面盖住 | Windows 上 WebView2 以原生 HWND 承载，压在所有 XAML 之上 | 宿主 `WebPluginView.UseOffscreenWebViewRendering`（默认 `true`）走离屏合成；异常时可置 `false` 回退 |
| dev 期 RPC 报「调试会话签发失败」 | 宿主是 Release 构建 | 改用 Debug 宿主；Release 无 `/__lybox/debug/session/*` |
| dev 期 RPC 502 / 连不上 | 两端 `LYBOX_WEB_PORT` 不一致 | 启动宿主前设 `$env:LYBOX_WEB_PORT="58080"` |
| 原生对话框不弹 | `ZitieNativeHost` 未 Attach | 确认页面走的是 `ZitiePage`；`StorageProvider` 需活动 TopLevel |
| 导出后没文件 | 用户取消保存对话框 | 前端返回 `null`，UI 提示「已取消导出」 |
| 中文 TXT 乱码 | 文件是 GBK 编码 | 宿主已实现 UTF-8 → GBK 回退；若仍乱码则属其他编码（如 Big5） |
| 打包后前端是旧版本 | `ui/dist` 未重建 | 确认 csproj 的 `<ZitieUiDir>ui</ZitieUiDir>` 存在，build.cs 会自动构建 |
| csproj 报 MSB4025 | 文件无 BOM，中文 locale 下按 GBK 解析 | 确保 csproj 带 UTF-8 BOM |

---

## 数据

| 数据 | 位置 | 规模 |
|------|------|------|
| 汉字笔顺 | `ui/public/data/hanzi-data/{字}.json` | 942 字 / 4.3 MB |
| 汉字拼音 | `ui/src/core/pinyin.ts` | 936 字（自动生成，勿手改） |
| PDF 库 | `ui/public/vendor/jspdf.js` | 374 KB（懒加载） |

扩充笔顺：编辑 `ui/tools/charset.js` 后跑 `pnpm fetch:strokes`。

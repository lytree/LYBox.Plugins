# 字帖生成器 · 离线版

对标 [z2h.cn](https://z2h.cn) 的功能复刻，**完全离线运行**。

技术栈：**Vue 3.5 + TypeScript + TSX（vue-jsx）+ Vite 6**，无 SFC、无 UI 框架依赖。
包管理：**pnpm 12**。

## 快速开始

```bash
pnpm install
pnpm dev              # 开发模式 -> http://127.0.0.1:5178
```

生产构建：

```bash
pnpm build            # 类型检查 + 打包 -> dist/
pnpm preview          # 预览构建产物
```

> 项目已通过 `packageManager` 字段锁定 pnpm 版本（Corepack 兼容）。
> 必须通过 HTTP 访问（笔顺数据用 `fetch` 加载 JSON），直接双击 `index.html` 无法工作。
> 构建产物 `dist/` 为纯静态文件，可直接部署到任意静态服务器。

### 全部脚本

| 命令 | 说明 |
|------|------|
| `pnpm dev` | 启动开发服务器 |
| `pnpm build` | 类型检查 + 生产构建 |
| `pnpm preview` | 预览 `dist/` |
| `pnpm typecheck` | 仅类型检查（strict） |
| `pnpm verify` | 渲染完整性检查（需先启动 dev） |
| `pnpm fetch:strokes` | 抓取笔顺数据 |

## 功能清单

### 10 种字帖类型

| 类型 | 说明 | 默认格子 |
|------|------|---------|
| 汉字 | 逐字练写，连续排布自动折行 | 田字格 |
| 词组 | 每词一行，边写边记 | 米字格 |
| 段落 | 粘贴任意文本，按内容折行 | 田字格 |
| 笔画 | 20 种基本笔画，可点选插入 | 田字格 |
| 字母 | A-Z 逐个练写 | 四线格 |
| 单词 | 每词一行，四线三格 | 四线格 |
| 英文段落 | 整段英文书写 | 四线格 |
| 数字 | 0-9 数字工整 | 四线格 |
| 拼音 | 带声调音节练习 | 田字格 |
| 控笔 | 横竖斜线、波浪、螺旋等轨迹 | 田字格 |

### 格子类型（10 种）

中文 7 种：作文格 / 田字格 / 米字格 / 回宫格 / 九宫格 / 回田格 / 回米格
英文 3 种：四线格 / 两线格 / 单线格

### 参数控制

- **显示**：笔顺、拼音、首字高亮、页码
- **排版**：方格类型、方格大小(6–30mm)、行间距(0–10mm)、插入空行/空列、上下偏移
- **描红**：描红数量（上限自动=每行列数）、描红/线条/文字/拼音四色可调
- **字体**：楷体/华文行楷/黑体/宋体/仿宋/微软雅黑/华文宋体；英文 6 款；常规/加粗；字号 30–110%
- **页面**：A4 / A5 / B5 / Letter，四边页边距独立设置
- **视图**：缩放 25%–200%

### 输入

- **直接输入**：文本框粘贴/键入内容，按类型分词
- **导入 TXT**：侧边栏「导入 TXT」按钮选择 `.txt` 文件，**原样保留换行、缩进与符号**（仅统一换行符、去 BOM 与行尾空白），按中英文自动切换到「段落」或「英文段落」；UTF-8 优先，自动回退 GBK 解码

### 输出

- **打印**：A4 无边距，浏览器打印（支持另存 PDF）
- **PDF 预览**：工具栏「PDF 预览」实时生成 PDF Blob，弹窗内通过浏览器内置查看器直接查看，可翻页浏览后再下载
- **导出 PDF**：jsPDF 按需懒加载，页面尺寸与纸张一致，直接下载
- **导出 PNG**：每页一张，原始分辨率

## 技术架构

### 目录结构

```
index.html               Vite 入口
vite.config.ts           构建配置（@vitejs/plugin-vue-jsx）
tsconfig.json            strict 模式
package.json             packageManager 锁定 pnpm 版本
pnpm-workspace.yaml      pnpm 12 配置（构建脚本白名单）
.npmrc                   镜像源配置
src/
  main.tsx               应用挂载
  types.ts               全部类型定义（单一类型源）
  components/
    App.tsx              根组件（状态编排 + 导出调度）
    Sidebar.tsx          参数面板（最大组件，绑定全局状态）
    Toolbar.tsx          工具栏（打印/导出/缩放）
    TypeTabs.tsx         类型切换标签
    PagePreview.tsx      canvas 渲染调度 + 防抖 watch
    PdfPreviewModal.tsx  PDF 实时生成 + 预览弹窗（iframe 调用内置查看器）
    controls.tsx         9 个通用控件（Switch/Slider/Select/Color/Number/TextArea/Button/Row/GroupTitle）
  core/
    cells.ts             10 种格子的 canvas 绘制实现
    renderer.ts          ZitieRenderer 类：布局计算 / 矩阵构建 / 逐格绘制
    stroke-data.ts       笔顺懒加载（24 并发 + 缓存）+ 笔画表 + 控笔模板
    txt-import.ts         TXT 导入（保留换行/缩进/符号 + 语言识别）
    pinyin.ts            拼音数据（936 字，自动生成，勿手改）
    config.ts            纸张 / 字体 / 字帖类型常量
    parse.ts             输入文本解析（按类型分词）
    store.ts             Vue reactive 全局状态 + 派生 computed
    exporter.ts          PNG / PDF / 打印
    url-params.ts        URL 参数预填
  styles/main.css        全局样式（含 @media print）
public/
  data/hanzi-data/*.json 942 字笔顺 SVG 路径
  vendor/jspdf.js        PDF 库（懒加载）
tools/
  charset.js             汉字表（增补笔顺数据用）
  fetch-strokes.js       笔顺数据抓取
  shot.mjs               无头截图
  verify-export.mjs      渲染完整性检查
  verify-jspdf.mjs       jsPDF 资源检查
```

### 状态管理

采用 **Vue reactive 单例 + watch 自动调度**：

- `src/core/store.ts` 导出全局 `state`（`reactive` 对象）
- `Sidebar.tsx` 中的控件通过 `v-model` 语义直接读写该对象
- `PagePreview.tsx` 用 `watch(() => [大量依赖字段])` + 80ms 防抖触发重绘
- 笔顺数据缓存**不放入 reactive**，避免 942 条路径的深度响应式开销

### 渲染管线

```
输入文本
  → parse.ts 按类型解析为「行」序列
  → stroke-data.ts 按需加载笔顺（懒加载 + 缓存）
  → renderer.ts buildMatrix() 自动折行 + 插入空行空列
  → paginate() 按每页容量切片
  → ZitieRenderer.renderPage() 逐格绘制
```

行内布局为三段式：`[笔顺区][拼音区][方格区]`，仅第一行前 N 格为浅色描红范字（按坐标判定，避免重复字符误判）。

## URL 参数

便于分享或批量导出：

```
/?type=hanzi&text=永和字&cell=mzg&size=12&trace=5&pinyin=1&bishun=1&zoom=1
```

| 参数 | 取值 |
|------|------|
| `type` | hanzi / cizu / duanluo / bihua / alphabet / word / paragraph / number / pinyin / pen |
| `text` | 内容（URL 编码） |
| `cell` | zwg / tzg / mzg / hgg / jgg / htg / hmg / sxg / lxg / dxg |
| `size` `gap` | 方格大小 / 行间距（mm） |
| `trace` | 描红数量 |
| `scale` `offset` `font` | 字号% / 偏移% / 字体 |
| `bishun` `pinyin` `highlight` `pagenum` `brow` `bcol` | 0 或 1 |
| `paper` | A4 / A5 / B5 / Letter |
| `zoom` | 0.25–2 |

## 离线数据

| 数据 | 位置 | 规模 |
|------|------|------|
| 汉字笔顺 | `public/data/hanzi-data/{字}.json` | 942 字 / 4.1 MB |
| 汉字拼音 | `src/core/pinyin.ts` | 936 字 |
| PDF 库 | `public/vendor/jspdf.js` | 374 KB |

笔顺为 SVG path（1024×1024 基准），按需懒加载并缓存。补充新字：

```bash
# 编辑 tools/charset.js 增删汉字，然后：
pnpm fetch:strokes
```

## 测试

```bash
pnpm typecheck                # 类型检查（strict）
pnpm build                    # 类型检查 + 构建
pnpm verify                   # 渲染完整性（需先启动 dev）
```

## 与原站的差异

- **字体**：原站用授权在线楷体（田英章、庞中华等），本项目改用本机字体，效果取决于系统已装字体
- **笔顺**：原站支持逐笔动画演示，本项目为静态首笔提示 + 精确字形路径
- **拼音**：覆盖 936 常用字，超出范围不注音（回退正常显示）
- **数据量**：笔顺含 942 字，扩展只需重跑 `fetch-strokes.js`
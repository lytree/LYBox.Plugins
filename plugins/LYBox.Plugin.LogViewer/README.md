# Serilog Log Viewer 插件

**只支持 Serilog 的结构化日志**（JSON Lines / Compact JSON CLEF / JsonFormatter 输出）：
自动识别、聚合多文件、按级别与属性过滤、实时跟踪新增内容、导出 CSV。

> 设计参照 [Analogy Log Viewer](https://github.com/Analogy-LogViewer/Analogy.LogViewer) 的
> provider/parser 分层与单视图聚合范式，按 LYBox 插件模型重写为原生 Avalonia 页面。

---

## 支持的输入格式

只认 Serilog 的**结构化 JSON**输出，两种官方形态都支持：

| 形态 | 产出方式 | 字段 |
|---|---|---|
| Compact JSON（CLEF） | `Serilog.Formatting.Compact.CompactJsonFormatter` | `@t` `@l` `@mt` `@m` `@i` `@x` `@r` + 顶层属性 |
| JsonFormatter | `Serilog.Formatting.Json.JsonFormatter` | `Timestamp` `Level` `MessageTemplate` `RenderedMessage` `Properties` `Exception` |

判定规则：JSON 对象必须含**时间 / 级别 / 消息 / 消息模板**之一（Serilog 的核心字段），
否则视为普通业务 JSON 而跳过。文件有内容却一条都没解析出来时，界面会在来源列表上提示
「未发现 Serilog 结构化日志」。

**不支持**：Serilog 控制台文本（`[12:34:56 INF] msg`）、NLog / log4j / log4net、CSV/TSV、XML、
纯文本兜底解析——这些解析器已全部移除。

---

## 功能

| 能力 | 说明 |
|---|---|
| 结构化属性 | `Properties` 嵌套对象与顶层 Enrich 属性都会被展开成 `Name = Value`，在详情面板逐条展示；网格消息列右侧灰色显示消息模板 |
| 消息模板 | `@mt` / `MessageTemplate` 单独保留，可区分「渲染后的文本」与「模板」 |
| 级别过滤 | Trace/Debug/Info/Warn/Error/Fatal 可独立开关，chip 上实时显示各级计数 |
| 属性过滤 | 搜索框写 `OrderId=10086` 即按结构化属性精确过滤（左侧必须是已知属性名） |
| 全文检索 | 子串（默认）/ 正则（`.*`）/ 区分大小写（`Aa`），覆盖消息、模板、模块、线程、异常、属性名与属性值 |
| 多文件聚合 | 多个日志源合并到同一视图，序号连续；左侧可逐个移除 |
| 实时跟踪 | 打开「实时跟踪」后按 900ms 轮询增量，只读新增字节；日志轮转（文件变短）自动从头重读 |
| 行级着色 | 级别徽章 + 整行淡色底，浅色/深色主题下都可读 |
| 详情面板 | 时间、模块、消息、消息模板、事件 ID、结构化属性、原始行，全部可选文本 |
| 导出 | 当前视图导出 CSV，额外带 `Template` 与 `Properties`（JSON 对象）两列，UTF-8 BOM |
| 复制 | 多选后「复制」输出「时间 [级别] 来源 : 消息」格式 |
| 拖放 | 把日志文件直接拖进页面即可加载 |
| 本地化 | 中英双语（`Resources/Strings.resx` + `Strings.zh-CN.resx`） |

大数据量处理：20 000 行/批分块解析并让出 UI 线程；集合更新走 `BulkObservableCollection`
合并通知；网格用虚拟化列表。自测覆盖 45 000 行分页加载。

---

## 目录结构

```
LYBox.Plugin.LogViewer/
├── LYBox.Plugin.LogViewer.csproj    # PluginId / 版本 / 图标资源
├── LogViewerPlugin.cs               # [GenerateMetadata] 入口
├── PluginIcons.axaml                # 菜单图标
├── samples/                         # 示例日志（随构建复制到输出目录）
│   ├── serilog-clef.jsonl           # Compact JSON (CLEF)
│   └── serilog-jsonformatter.jsonl  # JsonFormatter
├── Models/
│   ├── LogLevel.cs                  # 级别枚举 + 短标签
│   ├── LogEntry.cs                  # 一条日志（含 Template / Properties）
│   ├── LogProperty.cs               # 一条结构化属性
│   └── LogSource.cs                 # 日志源：文件 + 游标 + 解析器实例
├── Parsing/
│   ├── ILogParser.cs                # Detect(打分) / Parse(批量)
│   ├── LogParserRegistry.cs         # 注册表（当前只有 Serilog 一个解析器）
│   ├── SerilogStructuredParser.cs   # Serilog 结构化日志解析器
│   ├── StructuredFieldMapper.cs     # Serilog 字段 → LogEntry
│   ├── LogValueParser.cs            # 时间戳 / 级别解析
│   ├── LogFileReader.cs             # 按字节切行的增量读取器（支持 tail）
│   └── LogParseContext.cs           # 解析上下文（序号分配等）
├── Services/
│   ├── LogLoadService.cs            # 分批加载 / 增量读取
│   ├── BulkObservableCollection.cs  # 批量集合通知
│   └── CsvExporter.cs
├── ViewModels/LogViewerPageViewModel.cs
├── Pages/LogViewerPage.axaml(.cs)
├── Converters/LogLevelConverters.cs # 级别配色（徽章/行底/文字）
└── Resources/Strings*.resx
```

---

## Serilog 字段映射

键匹配不敏感大小写与分隔符（`@` `_` `-` `.` 会被规范化），因此以下写法等价：

| 语义 | 识别的键 | 说明 |
|---|---|---|
| 时间 | `@t` `Timestamp` `Time` `ts` `utc` | Unix 秒/毫秒与常见日期串都支持 |
| 级别 | `@l` `Level` `lvl` | Serilog 级别名 + 缩写 + 数字级别（0~5） |
| 渲染消息 | `@m` `Message` `msg` `RenderedMessage` | 网格消息列显示它 |
| 消息模板 | `@mt` `MessageTemplate` `template` | 详情面板与网格灰色后缀 |
| 异常 | `@x` `Exception` | 多行堆栈完整保留 |
| 事件 ID | `@i` `EventId` | |
| 渲染补充 | `@r` `Renderings` | 原始 JSON 片段 |
| logger | `SourceContext` `Logger` `LoggerName` | |
| 线程 | `Thread` `ThreadId` `ThreadName` | |
| 属性容器 | `Properties` `PropertiesDict` | 嵌套对象展开为结构化属性 |
| 其余顶层键 | 任意 | 视为结构化属性（`Enrich.WithProperty` 的输出） |

---

## 扩展另一种结构化格式

1. 实现 `ILogParser`：`Detect()` 给样本打分（0~100），`Parse()` 批量出条目。
2. 在 `LogParserRegistry.s_parsers` 里注册**工厂**（不是单例——解析器通常带跨批缓冲状态）。
3. 字段语义可复用 `StructuredFieldMapper`（未知键会自动变成结构化属性）。

---

## 调试

```powershell
# 构建插件
dotnet build plugins/LYBox.Plugin.LogViewer/LYBox.Plugin.LogViewer.csproj -c Debug

# VS Code：F5 → "Debug Plugin - LogViewer"
# （需要宿主已构建过一次：Debug Host）
```

插件加载后位于侧边栏「Serilog 结构化日志」。输出目录下的
`samples/serilog-clef.jsonl` 与 `samples/serilog-jsonformatter.jsonl` 可直接拖进页面试用。

---

## 已知限制

- **UTF-16 文件的实时跟踪不精确**：宽字符文件走整文件解码路径（偏移按文件末尾计算），
  切换文件后重新打开即可；UTF-8 / GBK / ASCII 的增量读取按字节精确。
- **Serilog 控制台文本格式不支持**：只处理结构化 JSON 输出。
- 列宽固定（时间/级别/来源/模块 + 弹性消息列），暂不支持拖拽调整列宽或自定义追加列。

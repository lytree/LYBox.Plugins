using System.Globalization;

namespace LYBox.Plugin.LogViewer.Models;

/// <summary>
/// 一条已解析的日志消息。构建期可变（解析器会追加异常续行），
/// 发布到 UI 后视为只读——不实现 INotifyPropertyChanged，避免百万行场景下的通知开销。
/// </summary>
public sealed class LogEntry
{
    private IReadOnlyDictionary<string, string>? _propertyMap;

    public long Index { get; init; }

    /// <summary>源文件内的行号（1 基）。</summary>
    public int LineNumber { get; init; }

    public DateTimeOffset? Timestamp { get; init; }

    public LogLevel Level { get; init; } = LogLevel.Unknown;

    /// <summary>来源文件名（多文件聚合时用于区分）。</summary>
    public string SourceName { get; init; } = string.Empty;

    /// <summary>来源文件完整路径，导出与「在文件夹中显示」使用。</summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>logger / category / 命名空间。</summary>
    public string? Module { get; init; }

    public string? Thread { get; init; }

    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 消息模板（Serilog 的 <c>@mt</c> / <c>MessageTemplate</c>），形如
    /// <c>Order {OrderId} shipped</c>。渲染后的文本在 <see cref="Message"/>。
    /// </summary>
    public string? Template { get; init; }

    /// <summary>Serilog 事件 ID（<c>@i</c> / <c>EventId</c>）。</summary>
    public string? EventId { get; init; }

    /// <summary>Serilog 的渲染补充（<c>@r</c>），保存原始 JSON 片段。</summary>
    public string? Renderings { get; init; }

    /// <summary>
    /// 结构化属性：Serilog <c>Properties</c> 里的键值、顶层 Enrich 属性、
    /// CSV 的额外列等。保持出现顺序，便于按日志原始顺序展示。
    /// </summary>
    public List<LogProperty> PropertyList { get; init; } = [];

    /// <summary>结构化属性的字典视图（按名称忽略大小写查找）。</summary>
    public IReadOnlyDictionary<string, string> Properties =>
        _propertyMap ??= PropertyList.ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);

    /// <summary>异常信息与堆栈（多行日志的续行会汇总到这里）。</summary>
    public string? Exception { get; set; }

    /// <summary>原始行文本（首行）。</summary>
    public string RawLine { get; init; } = string.Empty;

    public string TimeDisplay => Timestamp is { } ts
        ? ts.ToString("MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
        : string.Empty;

    public string SortedTimeKey => Timestamp?.UtcTicks.ToString("D20", CultureInfo.InvariantCulture) ?? string.Empty;

    public string LevelText => LogLevelText.ToShortText(Level);

    public string ModuleDisplay => Module ?? string.Empty;

    public string ThreadDisplay => Thread ?? string.Empty;

    public bool HasException => !string.IsNullOrWhiteSpace(Exception);

    public bool HasProperties => PropertyList.Count > 0;

    /// <summary>
    /// 结构化属性的文本视图（每行 <c>Name = Value</c>）。
    /// 详情面板用它渲染：Avalonia 12 的 ItemsControl 在无限高度容器中测得 0 高度，
    /// 直接拼文本更稳，也方便整段复制。
    /// </summary>
    public string PropertiesText => string.Join(Environment.NewLine, PropertyList.Select(p => p.Display));

    public bool HasTemplate => !string.IsNullOrWhiteSpace(Template);

    /// <summary>把无法独立成条的续行（异常堆栈等）挂到当前条目上。</summary>
    internal void AppendContinuation(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Exception = Exception is null ? text : Exception + Environment.NewLine + text;
    }
}

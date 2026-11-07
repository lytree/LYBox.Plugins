using LYBox.Plugin.LogViewer.Models;

namespace LYBox.Plugin.LogViewer.Parsing;

/// <summary>
/// 单次解析调用的上下文：来源信息、序号分配、无日期时间戳的补全基准。
/// </summary>
public sealed class LogParseContext
{
    private long _cursor;

    public required string SourceName { get; init; }

    public required string SourcePath { get; init; }

    public required long StartIndex { get; init; }

    /// <summary>文件最后写入时间，用于补全「只有时间没有日期」的日志行。</summary>
    public DateTimeOffset? DateHint { get; init; }

    public long NextIndex() => StartIndex + _cursor++;

    public LogEntry CreateEntry(
        RawLine line,
        DateTimeOffset? timestamp = null,
        LogLevel level = LogLevel.Unknown,
        string? message = null,
        string? module = null,
        string? thread = null,
        string? exception = null,
        string? template = null,
        string? eventId = null,
        string? renderings = null,
        IReadOnlyList<LogProperty>? properties = null)
    {
        return new LogEntry
        {
            Index = NextIndex(),
            LineNumber = line.Number,
            Timestamp = timestamp,
            Level = level,
            SourceName = SourceName,
            SourcePath = SourcePath,
            Module = string.IsNullOrWhiteSpace(module) ? null : module.Trim(),
            Thread = string.IsNullOrWhiteSpace(thread) ? null : thread.Trim(),
            Message = message ?? line.Text,
            Template = string.IsNullOrWhiteSpace(template) ? null : template,
            EventId = string.IsNullOrWhiteSpace(eventId) ? null : eventId,
            Renderings = string.IsNullOrWhiteSpace(renderings) ? null : renderings,
            // 物化成 List：ItemsControl.ItemsSource 需要非泛型 IEnumerable。
            PropertyList = properties is null ? [] : [.. properties],
            Exception = string.IsNullOrWhiteSpace(exception) ? null : exception,
            RawLine = line.Text,
        };
    }
}

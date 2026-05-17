namespace Avalonia.Plugin.TDLSharp.Models;

public enum LogLevel
{
    Information,
    Warning,
    Error,
    Debug,
    Trace
}

public class LogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string Message { get; init; } = string.Empty;
    public LogLevel Level { get; init; } = LogLevel.Information;

    public string LevelIcon => Level switch
    {
        LogLevel.Information => "ℹ",
        LogLevel.Warning => "⚠",
        LogLevel.Error => "✖",
        LogLevel.Debug => "🔍",
        LogLevel.Trace => "·",
        _ => "·"
    };
}

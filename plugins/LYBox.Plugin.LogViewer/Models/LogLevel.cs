namespace LYBox.Plugin.LogViewer.Models;

/// <summary>日志级别。数值用于「最低级别」过滤，顺序与常见日志框架一致。</summary>
public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warn = 3,
    Error = 4,
    Fatal = 5,

    /// <summary>无法识别级别的行（如纯文本续行、非结构化内容）。</summary>
    Unknown = 6,
}

public static class LogLevelText
{
    /// <summary>级别短标签。用于网格徽章，保持等宽不跳动。</summary>
    public static string ToShortText(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Info => "INF",
        LogLevel.Warn => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Fatal => "FTL",
        _ => "—",
    };

    /// <summary>级别全称（导出 CSV 时使用，便于二次分析）。</summary>
    public static string ToFullText(LogLevel level) => level switch
    {
        LogLevel.Trace => "Trace",
        LogLevel.Debug => "Debug",
        LogLevel.Info => "Info",
        LogLevel.Warn => "Warning",
        LogLevel.Error => "Error",
        LogLevel.Fatal => "Fatal",
        _ => "Unknown",
    };
}

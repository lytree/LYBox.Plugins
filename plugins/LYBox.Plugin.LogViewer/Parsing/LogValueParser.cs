using System.Globalization;
using System.Text.RegularExpressions;
using LYBox.Plugin.LogViewer.Models;

namespace LYBox.Plugin.LogViewer.Parsing;

/// <summary>时间戳 / 级别字符串的通用解析工具。</summary>
public static partial class LogValueParser
{
    private static readonly string[] s_formats =
    [
        "yyyy-MM-dd HH:mm:ss.fffffff",
        "yyyy-MM-dd HH:mm:ss.ffffff",
        "yyyy-MM-dd HH:mm:ss.ffff",
        "yyyy-MM-dd HH:mm:ss.fff",
        "yyyy-MM-dd HH:mm:ss.ff",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.fffffff",
        "yyyy-MM-ddTHH:mm:ss.ffffff",
        "yyyy-MM-ddTHH:mm:ss.fff",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy/MM/dd HH:mm:ss.fff",
        "yyyy/MM/dd HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss.fff zzz",
        "yyyy-MM-dd HH:mm:ss zzz",
        "MM-dd HH:mm:ss.fff",
        "MM-dd HH:mm:ss",
        "HH:mm:ss.fffffff",
        "HH:mm:ss.ffffff",
        "HH:mm:ss.fff",
        "HH:mm:ss",
    ];

    /// <summary>Serilog 的级别名与常见缩写。</summary>
    private static readonly Dictionary<string, LogLevel> s_levels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["trace"] = LogLevel.Trace,
        ["trc"] = LogLevel.Trace,
        ["verbose"] = LogLevel.Trace,
        ["vrb"] = LogLevel.Trace,
        ["v"] = LogLevel.Trace,

        ["debug"] = LogLevel.Debug,
        ["dbg"] = LogLevel.Debug,
        ["d"] = LogLevel.Debug,

        ["information"] = LogLevel.Info,
        ["info"] = LogLevel.Info,
        ["inf"] = LogLevel.Info,
        ["i"] = LogLevel.Info,

        ["warning"] = LogLevel.Warn,
        ["warn"] = LogLevel.Warn,
        ["wrn"] = LogLevel.Warn,
        ["w"] = LogLevel.Warn,

        ["error"] = LogLevel.Error,
        ["err"] = LogLevel.Error,
        ["e"] = LogLevel.Error,

        ["fatal"] = LogLevel.Fatal,
        ["ftl"] = LogLevel.Fatal,
        ["f"] = LogLevel.Fatal,
    };

    [GeneratedRegex(@"(?<=\d),(?=\d{3})")]
    private static partial Regex CommaFractionRegex();

    [GeneratedRegex(@"^\d{10}$|^\d{13}$")]
    private static partial Regex UnixSecondsRegex();

    /// <summary>
    /// 解析时间戳。支持 ISO / 常见格式 / 逗号小数 / Unix 秒与毫秒 / 纯时间（用 hint 补日期）。
    /// </summary>
    public static DateTimeOffset? ParseTimestamp(string? text, DateTimeOffset? hint = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var value = text.Trim().Trim('[', ']', '(', ')');

        if (UnixSecondsRegex().IsMatch(value) && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
        {
            try
            {
                return value.Length == 13
                    ? DateTimeOffset.FromUnixTimeMilliseconds(unix).ToLocalTime()
                    : DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime();
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        var normalized = CommaFractionRegex().Replace(value, ".");

        if (DateTimeOffset.TryParseExact(normalized, s_formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out var exact))
        {
            return exact;
        }

        if (DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out var loose))
        {
            return loose;
        }

        return null;
    }

    /// <summary>判断原始时间戳文本里是否已包含日期部分（形如 2024-05-01）。</summary>
    public static bool HasDatePart(string? raw)
    {
        return !string.IsNullOrWhiteSpace(raw) && DatePartRegex().IsMatch(raw);
    }

    [GeneratedRegex(@"\d{4}[-/]\d{1,2}[-/]\d{1,2}")]
    private static partial Regex DatePartRegex();

    /// <summary>
    /// 统一处理时间戳收尾：原文没有日期部分时，用文件写入日期补全，避免跨天排序错乱。
    /// </summary>
    public static DateTimeOffset? FinalizeTimestamp(DateTimeOffset? timestamp, string? raw, DateTimeOffset? hint)
    {
        if (timestamp is null)
        {
            return null;
        }

        return HasDatePart(raw) ? timestamp : AttachDate(timestamp, hint);
    }

    /// <summary>把「只有时间没有日期」的时间戳补上日期（hint 为文件写入时间）。</summary>
    public static DateTimeOffset? AttachDate(DateTimeOffset? timestamp, DateTimeOffset? hint)
    {
        if (timestamp is null || hint is null)
        {
            return timestamp;
        }

        return new DateTimeOffset(
            hint.Value.Date.Add(timestamp.Value.TimeOfDay),
            hint.Value.Offset);
    }

    public static LogLevel ParseLevel(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return LogLevel.Unknown;
        }

        var key = text.Trim().Trim('[', ']', '(', ')', ':', '-', '<', '>', '|');

        if (s_levels.TryGetValue(key, out var level))
        {
            return level;
        }

        // Microsoft.Extensions.Logging 允许用数字表示级别（0=Trace … 5=Fatal）。
        if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            return numeric switch
            {
                0 => LogLevel.Trace,
                1 => LogLevel.Debug,
                2 => LogLevel.Info,
                3 => LogLevel.Warn,
                4 => LogLevel.Error,
                5 => LogLevel.Fatal,
                _ => LogLevel.Unknown,
            };
        }

        return LogLevel.Unknown;
    }

    /// <summary>把日式/本地化日期串做最小清理后再解析。</summary>
    public static string CleanMessage(string? text)
    {
        return string.IsNullOrEmpty(text) ? string.Empty : text.TrimEnd();
    }
}

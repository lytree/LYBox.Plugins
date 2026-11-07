using LYBox.Plugin.LogViewer.Models;

namespace LYBox.Plugin.LogViewer.Parsing;

/// <summary>
/// Serilog 结构化字段到 <see cref="LogEntry"/> 的映射。
/// 只认 Serilog 的字段语义（CLEF 的 <c>@</c> 前缀与 JsonFormatter 的长名字），
/// 键匹配不敏感大小写与分隔符，因此 <c>@t</c> 与 <c>Timestamp</c> 都能命中；
/// 不属于已知字段的顶层键（例如 <c>Enrich.WithProperty</c> 注入的自定义属性）
/// 会自动成为结构化属性。
/// </summary>
public static class StructuredFieldMapper
{
    private static readonly string[] s_timestampKeys =
    [
        "t", "timestamp", "time", "ts", "utc",
    ];

    private static readonly string[] s_levelKeys =
    [
        "l", "level", "lvl",
    ];

    private static readonly string[] s_messageKeys =
    [
        "m", "message", "msg", "renderedmessage",
    ];

    private static readonly string[] s_messageTemplateKeys = ["mt", "messagetemplate", "template"];

    private static readonly string[] s_eventIdKeys = ["i", "eventid"];

    private static readonly string[] s_renderingsKeys = ["r", "renderings"];

    private static readonly string[] s_moduleKeys = ["sourcecontext", "logger", "loggername"];

    private static readonly string[] s_threadKeys = ["thread", "threadid", "threadname"];

    private static readonly string[] s_exceptionKeys = ["x", "exception"];

    /// <summary>属性容器的键名（其内容会被展开为结构化属性，而不是当成普通字段）。</summary>
    private static readonly string[] s_propertyContainerKeys = ["properties", "propertiesdict"];

    /// <summary>全部已知元字段（规范化后）。不在此集合中的键都会被视为结构化属性。</summary>
    private static readonly HashSet<string> s_knownFields = new(StringComparer.Ordinal)
    {
        "t", "timestamp", "time", "ts", "utc",
        "l", "level", "lvl",
        "m", "message", "msg", "renderedmessage",
        "mt", "messagetemplate", "template",
        "i", "eventid",
        "r", "renderings",
        "x", "exception",
        "sourcecontext", "logger", "loggername",
        "thread", "threadid", "threadname",
        "properties", "propertiesdict",
    };

    /// <summary>
    /// Serilog 日志的核心字段：时间、级别、消息或消息模板。
    /// 缺少这些字段的 JSON 对象不是 Serilog 日志（例如普通业务 JSON），解析器会跳过它们。
    /// </summary>
    private static readonly HashSet<string> s_coreFields = new(StringComparer.Ordinal)
    {
        "t", "timestamp", "time", "ts", "utc",
        "l", "level", "lvl",
        "m", "message", "msg", "renderedmessage",
        "mt", "messagetemplate", "template",
    };

    /// <summary>判断字段集合里是否含有 Serilog 的核心字段（时间/级别/消息/模板）。</summary>
    public static bool HasCoreField(IReadOnlyDictionary<string, string?> fields)
    {
        foreach (var (key, _) in fields)
        {
            if (s_coreFields.Contains(Normalize(key)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>判断一个（规范化后的）字段名是否属于已知元字段。</summary>
    public static bool IsKnownField(string normalizedKey) => s_knownFields.Contains(normalizedKey);

    /// <summary>判断字段是否为属性容器（需要展开内部键值）。</summary>
    public static bool IsPropertyContainer(string normalizedKey)
        => Array.IndexOf(s_propertyContainerKeys, normalizedKey) >= 0;

    /// <summary>
    /// 把字段字典映射为日志条目。
    /// </summary>
    /// <param name="fields">原始键名 → 原始值（键保持原样，便于属性展示时保留大小写）。</param>
    /// <param name="extraProperties">解析器额外收集的结构化属性（如 Serilog 的嵌套 Properties）。</param>
    /// <param name="line">原始行。</param>
    /// <param name="context">解析上下文。</param>
    public static LogEntry Map(
        IReadOnlyDictionary<string, string?> fields,
        IReadOnlyList<LogProperty>? extraProperties,
        RawLine line,
        LogParseContext context)
    {
        // 规范化视图：用于已知字段查找。
        var normalized = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, value) in fields)
        {
            var key2 = Normalize(key);
            if (!normalized.ContainsKey(key2))
            {
                normalized[key2] = value;
            }
        }

        var timestamp = ReadTimestamp(normalized, context.DateHint);
        var level = LogValueParser.ParseLevel(Read(normalized, s_levelKeys));

        var message = Read(normalized, s_messageKeys);
        var template = Read(normalized, s_messageTemplateKeys);

        if (string.IsNullOrWhiteSpace(message))
        {
            message = template;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            message = line.Text;
        }

        // 未知键 → 结构化属性（Serilog 顶层 Enrich 属性等）。
        var properties = new List<LogProperty>(extraProperties ?? []);
        var seen = new HashSet<string>(properties.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in fields)
        {
            if (value is null || IsKnownField(Normalize(key)))
            {
                continue;
            }

            if (seen.Add(key))
            {
                properties.Add(new LogProperty(key, value));
            }
        }

        return context.CreateEntry(
            line,
            timestamp,
            level,
            message,
            Read(normalized, s_moduleKeys),
            Read(normalized, s_threadKeys),
            Read(normalized, s_exceptionKeys),
            template,
            Read(normalized, s_eventIdKeys),
            Read(normalized, s_renderingsKeys),
            properties);
    }

    /// <summary>规范化字段名：去掉 <c>@</c> / <c>_</c> / <c>-</c> / <c>.</c> 并转小写。</summary>
    public static string Normalize(string name)
    {
        Span<char> buffer = name.Length <= 128 ? stackalloc char[name.Length] : new char[name.Length];
        var length = 0;

        foreach (var ch in name)
        {
            if (ch is '@' or '_' or '-' or '.' or ' ' or ':')
            {
                continue;
            }

            buffer[length++] = char.ToLowerInvariant(ch);
        }

        return new string(buffer[..length]);
    }

    private static string? Read(IReadOnlyDictionary<string, string?> normalized, string[] keys)
    {
        foreach (var key in keys)
        {
            if (normalized.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static DateTimeOffset? ReadTimestamp(IReadOnlyDictionary<string, string?> normalized, DateTimeOffset? hint)
    {
        var raw = Read(normalized, s_timestampKeys);
        if (raw is null)
        {
            return null;
        }

        return LogValueParser.FinalizeTimestamp(LogValueParser.ParseTimestamp(raw, hint), raw, hint);
    }
}

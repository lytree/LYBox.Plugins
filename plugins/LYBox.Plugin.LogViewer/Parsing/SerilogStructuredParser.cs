using System.Text;
using System.Text.Json;
using LYBox.Plugin.LogViewer.Models;

namespace LYBox.Plugin.LogViewer.Parsing;

/// <summary>
/// Serilog 结构化日志解析器。覆盖 Serilog 官方的两种 JSON 形态：
/// <list type="number">
/// <item>Compact JSON（CLEF）：<c>{"@t":"…","@l":"Information","@mt":"…","@m":"…","@i":"…","@x":"…","@r":[…]}</c></item>
/// <item>JsonFormatter（<c>Serilog.Formatting.Json</c> 默认输出）：
/// <c>{"Timestamp":"…","Level":"Information","MessageTemplate":"…","RenderedMessage":"…","Properties":{…},"Exception":null}</c></item>
/// </list>
/// 顶层由 <c>Enrich.WithProperty</c> 注入的自定义键会作为结构化属性保留。
/// 非 JSON 行会被跳过——本插件只认 Serilog 的结构化输出。
/// </summary>
public sealed class SerilogStructuredParser : ILogParser
{
    private static readonly string[] s_serilogMarkers =
    [
        "\"@t\"", "\"@l\"", "\"@mt\"", "\"@m\"", "\"@i\"", "\"@x\"", "\"@r\"",
        "\"Timestamp\"", "\"Level\"", "\"MessageTemplate\"", "\"RenderedMessage\"", "\"Properties\"",
    ];

    private readonly StringBuilder _pending = new();
    private int _depth;
    private bool _inString;
    private bool _escape;
    private int _pendingLineNumber;

    public string Key => "serilog-structured";

    public string DisplayName => "Serilog (structured JSON)";

    public int Detect(IReadOnlyList<string> sampleLines, string fileName)
    {
        var sampled = 0;
        var objectLines = 0;
        var serilogLines = 0;

        foreach (var line in sampleLines)
        {
            var trimmed = line.TrimStart();
            if (trimmed.Length == 0)
            {
                continue;
            }

            sampled++;

            if (!trimmed.StartsWith('{'))
            {
                continue;
            }

            objectLines++;

            foreach (var marker in s_serilogMarkers)
            {
                if (trimmed.Contains(marker, StringComparison.Ordinal))
                {
                    serilogLines++;
                    break;
                }
            }
        }

        if (sampled == 0)
        {
            return 0;
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var extensionBonus = extension is ".json" or ".jsonl" or ".ndjson" or ".clef" ? 12 : 0;

        if (serilogLines > 0 && objectLines > 0 && (double)serilogLines / objectLines >= 0.5)
        {
            return 90 + extensionBonus;
        }

        // 是 JSON 但看不出 Serilog 特征：给一个中等分，仍按结构化方式尝试解析。
        if (objectLines > 0 && (double)objectLines / sampled >= 0.6)
        {
            return 55 + extensionBonus;
        }

        return 0;
    }

    public List<LogEntry> Parse(IReadOnlyList<RawLine> lines, LogParseContext context)
    {
        var result = new List<LogEntry>(lines.Count);

        foreach (var line in lines)
        {
            var trimmed = line.Text.TrimStart();

            // 数组语法（[ ] ,）在对象之外时直接跳过。
            if (_depth == 0 && !trimmed.StartsWith('{'))
            {
                continue;
            }

            if (_pending.Length == 0)
            {
                _pendingLineNumber = line.Number;
            }

            _pending.AppendLine(line.Text);
            Scan(line.Text);

            if (_depth > 0)
            {
                continue;
            }

            var json = _pending.ToString().Trim();
            _pending.Clear();
            _depth = 0;
            _inString = false;
            _escape = false;

            // 数组元素结尾的逗号不属于对象本身，去掉后再解析。
            json = json.TrimEnd(',').TrimEnd();

            if (json.Length == 0)
            {
                continue;
            }

            var entry = TryCreateEntry(json, context);
            if (entry is not null)
            {
                result.Add(entry);
            }
        }

        return result;
    }

    private LogEntry? TryCreateEntry(string json, LogParseContext context)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
                MaxDepth = 32,
            });

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            // 顶层键原样保留：@t/@l/@mt/@m 与 Timestamp/Level/MessageTemplate/RenderedMessage 都会在这里被统一识别。
            var fields = new Dictionary<string, string?>(StringComparer.Ordinal);
            var extras = new List<LogProperty>();

            foreach (var property in document.RootElement.EnumerateObject())
            {
                var normalized = StructuredFieldMapper.Normalize(property.Name);

                // Serilog 的 Properties / PropertiesDict 是嵌套对象，展开成结构化属性。
                if (StructuredFieldMapper.IsPropertyContainer(normalized) && property.Value.ValueKind == JsonValueKind.Object)
                {
                    foreach (var nested in property.Value.EnumerateObject())
                    {
                        var value = ValueToString(nested.Value);
                        if (value is not null)
                        {
                            extras.Add(new LogProperty(nested.Name, value));
                        }
                    }

                    continue;
                }

                fields[property.Name] = ValueToString(property.Value);
            }

            // 没有时间/级别/消息/模板的 JSON 不是 Serilog 日志（例如普通业务 JSON），直接跳过。
            if (!StructuredFieldMapper.HasCoreField(fields))
            {
                return null;
            }

            var rawLine = new RawLine(_pendingLineNumber, json);
            return StructuredFieldMapper.Map(fields, extras, rawLine, context);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>把 JSON 标量转成展示用文本；对象/数组保留原始 JSON（过长时截断）。</summary>
    private static string? ValueToString(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                return element.GetRawText();
            case JsonValueKind.True:
                return "true";
            case JsonValueKind.False:
                return "false";
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            default:
                var raw = element.GetRawText();
                return raw.Length > 200 ? raw[..200] + "…" : raw;
        }
    }

    /// <summary>扫描字符维护对象嵌套深度（忽略字符串字面量里的括号）。</summary>
    private void Scan(string text)
    {
        foreach (var ch in text)
        {
            if (_inString)
            {
                if (_escape)
                {
                    _escape = false;
                }
                else if (ch == '\\')
                {
                    _escape = true;
                }
                else if (ch == '"')
                {
                    _inString = false;
                }

                continue;
            }

            switch (ch)
            {
                case '"':
                    _inString = true;
                    break;
                case '{':
                    _depth++;
                    break;
                case '}':
                    _depth--;
                    break;
            }
        }
    }
}

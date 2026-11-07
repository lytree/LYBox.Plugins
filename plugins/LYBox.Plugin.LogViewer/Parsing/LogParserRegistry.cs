using System.Text;

namespace LYBox.Plugin.LogViewer.Parsing;

/// <summary>
/// 解析器注册表。本插件只支持 Serilog 的结构化日志（JSON Lines / CLEF / JsonFormatter），
/// 因此注册表里只有一个解析器；用工厂而非单例——Serilog 解析器带跨批缓冲状态。
/// </summary>
public static class LogParserRegistry
{
    private static readonly (string Key, Func<ILogParser> Factory)[] s_parsers =
    [
        ("serilog-structured", static () => new SerilogStructuredParser()),
    ];

    public static IReadOnlyList<string> SupportedKeys { get; } = s_parsers.Select(p => p.Key).ToArray();

    public static ILogParser Create(string? key)
    {
        if (!string.IsNullOrEmpty(key))
        {
            foreach (var (candidate, factory) in s_parsers)
            {
                if (string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase))
                {
                    return factory();
                }
            }
        }

        return new SerilogStructuredParser();
    }

    /// <summary>
    /// 选出解析器。本插件只有一个候选，返回值恒为 Serilog 结构化解析器；
    /// <see cref="ILogParser.Detect"/> 的分数只用于判断文件「像不像」Serilog 结构化日志
    /// （分数为 0 表示不像，界面会提示未发现结构化日志）。
    /// </summary>
    public static ILogParser Detect(string path, IReadOnlyList<string>? sampleLines = null)
    {
        var samples = sampleLines ?? ReadSampleLines(path);
        var fileName = Path.GetFileName(path);

        ILogParser best = new SerilogStructuredParser();
        var bestScore = 0;

        foreach (var (_, factory) in s_parsers)
        {
            var parser = factory();
            int score;

            try
            {
                score = parser.Detect(samples, fileName);
            }
            catch (Exception)
            {
                continue;
            }

            if (score > bestScore)
            {
                best = parser;
                bestScore = score;
            }
        }

        return best;
    }

    /// <summary>读取文件头部若干行用于格式探测。</summary>
    public static IReadOnlyList<string> ReadSampleLines(string path, int maxLines = 40)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var lines = new List<string>(maxLines);

            while (lines.Count < maxLines && reader.ReadLine() is { } line)
            {
                lines.Add(line.Length > 4096 ? line[..4096] : line);
            }

            return lines;
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }
}

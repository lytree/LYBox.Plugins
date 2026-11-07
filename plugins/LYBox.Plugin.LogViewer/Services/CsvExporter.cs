using System.Text;
using System.Text.Json;
using LYBox.Plugin.LogViewer.Models;

namespace LYBox.Plugin.LogViewer.Services;

/// <summary>把当前视图导出为 CSV（UTF-8 BOM，Excel 直接双击可读）。</summary>
public static class CsvExporter
{
    private static readonly string[] s_headers =
    [
        "Index", "Time", "Level", "Source", "Module", "Thread", "Template", "Message", "Properties", "Exception", "RawLine",
    ];

    public static async Task ExportAsync(string path, IReadOnlyList<LogEntry> entries, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        await writer.WriteLineAsync(string.Join(',', s_headers)).ConfigureAwait(false);

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = string.Join(
                ',',
                Escape(entry.Index.ToString()),
                Escape(entry.Timestamp?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? string.Empty),
                Escape(LogLevelText.ToFullText(entry.Level)),
                Escape(entry.SourceName),
                Escape(entry.ModuleDisplay),
                Escape(entry.ThreadDisplay),
                Escape(entry.Template ?? string.Empty),
                Escape(entry.Message),
                Escape(SerializeProperties(entry)),
                Escape(entry.Exception ?? string.Empty),
                Escape(entry.RawLine));

            await writer.WriteLineAsync(line).ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>结构化属性序列化为 JSON 对象字符串（保持原始顺序）。</summary>
    private static string SerializeProperties(LogEntry entry)
    {
        if (!entry.HasProperties)
        {
            return string.Empty;
        }

        var payload = entry.PropertyList.ToDictionary(p => p.Name, p => p.Value);
        return JsonSerializer.Serialize(payload);
    }

    private static string Escape(string value)
    {
        if (value.Length == 0)
        {
            return string.Empty;
        }

        var needsQuote = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        if (!needsQuote)
        {
            return value;
        }

        return string.Concat("\"", value.Replace("\"", "\"\""), "\"");
    }
}

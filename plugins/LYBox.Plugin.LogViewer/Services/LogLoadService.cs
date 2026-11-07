using LYBox.Plugin.LogViewer.Models;
using LYBox.Plugin.LogViewer.Parsing;

namespace LYBox.Plugin.LogViewer.Services;

/// <summary>一批解析结果。</summary>
public sealed record LogLoadBatch(
    IReadOnlyList<LogEntry> Entries,
    long NewOffset,
    int NewLineNumber,
    bool HasMore,
    long FileSize);

/// <summary>
/// 分批加载入口：把「读文件 → 解析 → 序号分配」串起来，
/// 每次只处理一批行，避免大文件把 UI 卡死。
/// </summary>
public static class LogLoadService
{
    /// <summary>单批最多读取的行数。20k 行 ≈ 数 MB，解析耗时可控制在百毫秒级。</summary>
    public const int BatchLineCount = 20_000;

    public static LogLoadBatch LoadBatch(
        LogSource source,
        ILogParser parser,
        DateTimeOffset? dateHint,
        int maxLines = BatchLineCount)
    {
        var lines = new List<RawLine>(Math.Min(maxLines, 8192));

        using (var reader = new LogFileReader(source.FilePath, source.Offset, source.LineNumber))
        {
            reader.ReadLines(lines, maxLines, flushPartialLine: true);

            var context = new LogParseContext
            {
                SourceName = source.FileName,
                SourcePath = source.FilePath,
                StartIndex = source.NextIndex,
                DateHint = dateHint,
            };

            var entries = lines.Count == 0 ? [] : parser.Parse(lines, context);

            // 序号必须连续：解析器可能丢弃行（如 JSON 数组语法行），按实际产出推进。
            source.NextIndex += entries.Count;
            source.Offset = reader.Offset;
            source.LineNumber = reader.LineNumber;

            return new LogLoadBatch(
                entries,
                reader.Offset,
                reader.LineNumber,
                lines.Count >= maxLines,
                SafeFileSize(source.FilePath));
        }
    }

    /// <summary>读取新增内容（实时跟踪）：不 flush 半行，避免正在写入的行被重复输出。</summary>
    public static LogLoadBatch ReadIncrement(LogSource source, ILogParser parser, DateTimeOffset? dateHint)
    {
        var lines = new List<RawLine>(1024);

        using (var reader = new LogFileReader(source.FilePath, source.Offset, source.LineNumber))
        {
            reader.ReadLines(lines, BatchLineCount, flushPartialLine: false);

            if (lines.Count == 0)
            {
                return new LogLoadBatch([], source.Offset, source.LineNumber, false, SafeFileSize(source.FilePath));
            }

            var context = new LogParseContext
            {
                SourceName = source.FileName,
                SourcePath = source.FilePath,
                StartIndex = source.NextIndex,
                DateHint = dateHint,
            };

            var entries = parser.Parse(lines, context);
            source.NextIndex += entries.Count;
            source.Offset = reader.Offset;
            source.LineNumber = reader.LineNumber;

            return new LogLoadBatch(entries, reader.Offset, reader.LineNumber, false, SafeFileSize(source.FilePath));
        }
    }

    public static long SafeFileSize(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (IOException)
        {
            return 0;
        }
    }
}

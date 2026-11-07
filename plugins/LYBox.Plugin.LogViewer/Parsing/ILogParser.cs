using LYBox.Plugin.LogViewer.Models;

namespace LYBox.Plugin.LogViewer.Parsing;

/// <summary>
/// 日志格式解析器。一个解析器负责一类文件（文本行 / JSON / XML / CSV），
/// 通过 <see cref="Detect"/> 打分参与自动探测。
/// </summary>
public interface ILogParser
{
    /// <summary>稳定标识，用于持久化「用户手动指定的格式」。</summary>
    string Key { get; }

    string DisplayName { get; }

    /// <summary>
    /// 对样本行打分，0 表示完全不匹配。分数越高越优先。
    /// </summary>
    int Detect(IReadOnlyList<string> sampleLines, string fileName);

    /// <summary>把一批原始行解析为日志条目。实现应容忍残缺行并尽量保留原始内容。</summary>
    List<LogEntry> Parse(IReadOnlyList<RawLine> lines, LogParseContext context);
}

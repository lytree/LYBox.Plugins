namespace LYBox.Plugin.Downloader.Models;

public class LogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string Message { get; init; } = string.Empty;
    public string FormattedLine => $"[{Timestamp:HH:mm:ss}] {Message}";
}

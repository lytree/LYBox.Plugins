using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.TDLSharp.Models;

public partial class LogEntry : ObservableObject
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string Message { get; init; } = string.Empty;
    public string FormattedLine => $"[{Timestamp:HH:mm:ss}] {Message}";

    public bool IsProgress { get; init; }
    public string FileName { get; init; } = string.Empty;
    public long FileSize { get; init; }

    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private bool _isCompleted;
    [ObservableProperty] private bool _isFailed;
    [ObservableProperty] private string _statusText = string.Empty;
}

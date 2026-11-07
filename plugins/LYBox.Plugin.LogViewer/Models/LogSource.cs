using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using LYBox.Plugin.LogViewer.Parsing;

namespace LYBox.Plugin.LogViewer.Models;

/// <summary>
/// 一个已打开的日志源（磁盘文件 + 解析状态 + 增量读取游标）。
/// 需要参与 UI 绑定，因此继承 ObservableObject：加载进度、计数与跟踪状态会实时刷新。
/// </summary>
public sealed class LogSource : ObservableObject
{
    private string _parserKey = string.Empty;
    private string _parserName = string.Empty;
    private long _offset;
    private int _lineNumber;
    private long _nextIndex;
    private int _entryCount;
    private long _fileSize;
    private bool _isFollowing;
    private bool _isLoading;
    private bool _hasError;
    private string? _errorMessage;
    private string? _notice;

    public string FilePath { get; init; } = string.Empty;

    public string FileName => Path.GetFileName(FilePath);

    /// <summary>命中的解析器 Key。</summary>
    public string ParserKey
    {
        get => _parserKey;
        set => SetProperty(ref _parserKey, value);
    }

    /// <summary>解析器显示名，UI 上作为「格式」提示。</summary>
    public string ParserName
    {
        get => _parserName;
        set => SetProperty(ref _parserName, value);
    }

    /// <summary>已消费到的字节偏移，实时跟踪时从这里继续读。</summary>
    public long Offset
    {
        get => _offset;
        set => SetProperty(ref _offset, value);
    }

    /// <summary>已消费到的行号。</summary>
    public int LineNumber
    {
        get => _lineNumber;
        set => SetProperty(ref _lineNumber, value);
    }

    /// <summary>下一条日志的全局序号起点，保证多文件聚合时序号连续。</summary>
    public long NextIndex
    {
        get => _nextIndex;
        set => SetProperty(ref _nextIndex, value);
    }

    public int EntryCount
    {
        get => _entryCount;
        set
        {
            if (SetProperty(ref _entryCount, value))
            {
                OnPropertyChanged(nameof(CountDisplay));
            }
        }
    }

    public long FileSize
    {
        get => _fileSize;
        set
        {
            if (SetProperty(ref _fileSize, value))
            {
                OnPropertyChanged(nameof(SizeDisplay));
            }
        }
    }

    public bool IsFollowing
    {
        get => _isFollowing;
        set => SetProperty(ref _isFollowing, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>加载后的提示（例如「未发现 Serilog 结构化日志」）。</summary>
    public string? Notice
    {
        get => _notice;
        set => SetProperty(ref _notice, value);
    }

    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>该文件专用的解析器实例（CSV 表头、JSON 跨批缓冲等状态保存在实例内）。</summary>
    public ILogParser? Parser { get; set; }

    /// <summary>文件写入时间，用于补全没有日期的时间戳。</summary>
    public DateTimeOffset DateHint { get; set; } = DateTimeOffset.Now;

    public string SizeDisplay => FileSize switch
    {
        < 1024 => $"{FileSize} B",
        < 1024 * 1024 => $"{FileSize / 1024.0:0.#} KB",
        < 1024L * 1024 * 1024 => $"{FileSize / (1024.0 * 1024):0.#} MB",
        _ => $"{FileSize / (1024.0 * 1024 * 1024):0.##} GB",
    };

    public string CountDisplay => EntryCount.ToString("N0");
}

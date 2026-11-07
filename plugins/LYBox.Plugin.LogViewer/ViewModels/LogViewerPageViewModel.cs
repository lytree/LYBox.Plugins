using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LYBox.Plugin.LogViewer.Models;
using LYBox.Plugin.LogViewer.Pages;
using LYBox.Plugin.LogViewer.Parsing;
using LYBox.Plugin.LogViewer.Resources;
using LYBox.Plugin.LogViewer.Services;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;

namespace LYBox.Plugin.LogViewer.ViewModels;

/// <summary>
/// 日志查看器主视图模型：多文件聚合加载、格式自动探测、级别/关键字过滤、
/// 实时跟踪（tail）、CSV 导出与详情查看。
/// </summary>
[Menu("NAV_LogViewer", "KeyLogViewer", ParentKey = null, IconName = "FluentDocumentText20Regular", Order = 120)]
[NavigationItem("KeyLogViewer")]
[ViewMap(typeof(LogViewerPage))]
public partial class LogViewerPageViewModel : ViewModelBase
{
    private static readonly FilePickerFileType s_csvFileType = new("CSV")
    {
        Patterns = ["*.csv"],
        MimeTypes = ["text/csv"],
    };

    private static readonly FilePickerFileType s_logFileType = new("Serilog structured logs")
    {
        Patterns = ["*.json", "*.jsonl", "*.ndjson", "*.clef", "*.log", "*.txt"],
    };

    private readonly List<LogEntry> _entries = [];
    private readonly HashSet<string> _propertyNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _searchDebounce;
    private readonly DispatcherTimer _tailTimer;
    private int _busyCounter;
    private bool _tailBusy;
    private Regex? _activeRegex;
    private bool _regexFailed;
    private bool _propertyFilterActive;
    private string? _propertyFilterName;
    private string _propertyFilterValue = string.Empty;

    public LogViewerPageViewModel()
    {
        _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
        _searchDebounce.Tick += (_, _) =>
        {
            _searchDebounce.Stop();
            RebuildVisible();
        };

        _tailTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        _tailTimer.Tick += OnTailTick;

        Sources.CollectionChanged += OnSourcesChanged;
    }

    /// <summary>当前可见（通过过滤）的日志行。</summary>
    public BulkObservableCollection<LogEntry> VisibleEntries { get; } = new();

    /// <summary>已加载的日志源。</summary>
    public ObservableCollection<LogSource> Sources { get; } = new();

    [ObservableProperty] private LogEntry? _selectedEntry;
    [ObservableProperty] private bool _hasSelection;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _useRegex;
    [ObservableProperty] private bool _caseSensitive;

    [ObservableProperty] private bool _showTrace = true;
    [ObservableProperty] private bool _showDebug = true;
    [ObservableProperty] private bool _showInfo = true;
    [ObservableProperty] private bool _showWarn = true;
    [ObservableProperty] private bool _showError = true;
    [ObservableProperty] private bool _showFatal = true;
    [ObservableProperty] private bool _showUnknown = true;

    [ObservableProperty] private bool _autoScroll = true;
    [ObservableProperty] private bool _isFollowing;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = Strings.STATUS_Ready;

    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _visibleCount;

    [ObservableProperty] private int _traceCount;
    [ObservableProperty] private int _debugCount;
    [ObservableProperty] private int _infoCount;
    [ObservableProperty] private int _warnCount;
    [ObservableProperty] private int _errorCount;
    [ObservableProperty] private int _fatalCount;
    [ObservableProperty] private int _unknownCount;

    [ObservableProperty] private bool _hasSources;
    [ObservableProperty] private bool _hasVisibleEntries;

    /// <summary>给页面 code-behind 用的滚动通知（自动滚动到底部）。</summary>
    public event EventHandler? ScrollToEndRequested;

    // ---------------------------------------------------------------- 加载

    /// <summary>由「打开日志」按钮、拖放或外部调用统一进入。</summary>
    public async Task LoadFilesAsync(IEnumerable<string> paths)
    {
        var candidates = paths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .Where(p => Sources.All(s => !string.Equals(s.FilePath, p, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        var sources = new List<LogSource>(candidates.Count);
        foreach (var path in candidates)
        {
            var source = new LogSource
            {
                FilePath = path,
                IsFollowing = IsFollowing,
                DateHint = SafeLastWrite(path),
                FileSize = LogLoadService.SafeFileSize(path),
            };

            var parser = LogParserRegistry.Detect(path);
            source.Parser = parser;
            source.ParserKey = parser.Key;
            source.ParserName = parser.DisplayName;

            Sources.Add(source);
            sources.Add(source);
        }

        if (IsFollowing && !_tailTimer.IsEnabled)
        {
            _tailTimer.Start();
        }

        foreach (var source in sources)
        {
            await LoadSourceAsync(source);
        }
    }

    private async Task LoadSourceAsync(LogSource source)
    {
        if (source.Parser is null)
        {
            return;
        }

        BeginBusy();
        source.IsLoading = true;

        try
        {
            while (true)
            {
                var batch = await Task.Run(() =>
                    LogLoadService.LoadBatch(source, source.Parser, source.DateHint));

                if (batch.Entries.Count > 0)
                {
                    AppendEntries(batch.Entries);
                    source.EntryCount += batch.Entries.Count;
                }

                source.FileSize = batch.FileSize;

                if (!batch.HasMore)
                {
                    break;
                }

                // 让出 UI 线程，保证大批量加载时界面仍可交互。
                await Task.Yield();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            source.HasError = true;
            source.ErrorMessage = ex.Message;
            StatusText = Strings.Format("MSG_OpenFailed", ex.Message);
        }
        finally
        {
            source.IsLoading = false;

            // 文件有内容却一条结构化日志都没解析出来：多半不是 Serilog 的 JSON 输出。
            if (source.EntryCount == 0 && source.LineNumber > 0)
            {
                source.Notice = Strings.Get("MSG_NoStructuredLogs");
            }

            EndBusy();
        }
    }

    [RelayCommand]
    private async Task OpenFilesAsync()
    {
        var topLevel = GetTopLevel();
        if (topLevel is null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.BTN_OpenFiles,
            AllowMultiple = true,
            FileTypeFilter = [s_logFileType, FilePickerFileTypes.All],
        });

        var paths = files
            .Select(f => f.TryGetLocalPath())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .ToArray();

        if (paths.Length > 0)
        {
            await LoadFilesAsync(paths);
        }
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        var paths = Sources.Select(s => s.FilePath).ToArray();
        Clear();
        await LoadFilesAsync(paths);
    }

    [RelayCommand]
    private void Clear()
    {
        _entries.Clear();
        _propertyNames.Clear();
        VisibleEntries.ReplaceAll([]);
        Sources.Clear();

        TotalCount = 0;
        VisibleCount = 0;
        TraceCount = 0;
        DebugCount = 0;
        InfoCount = 0;
        WarnCount = 0;
        ErrorCount = 0;
        FatalCount = 0;
        UnknownCount = 0;
        SelectedEntry = null;
        HasVisibleEntries = false;
        UpdateStatus();
    }

    [RelayCommand]
    private void RemoveSource(LogSource? source)
    {
        if (source is null || !Sources.Remove(source))
        {
            return;
        }

        _entries.RemoveAll(e => string.Equals(e.SourcePath, source.FilePath, StringComparison.OrdinalIgnoreCase));
        RecalculateCounts();
        RebuildVisible();
    }

    // ---------------------------------------------------------------- 过滤

    partial void OnSearchTextChanged(string value)
    {
        _searchDebounce.Stop();
        _searchDebounce.Start();
    }

    partial void OnUseRegexChanged(bool value) => RebuildVisible();

    partial void OnCaseSensitiveChanged(bool value) => RebuildVisible();

    partial void OnShowTraceChanged(bool value) => RebuildVisible();

    partial void OnShowDebugChanged(bool value) => RebuildVisible();

    partial void OnShowInfoChanged(bool value) => RebuildVisible();

    partial void OnShowWarnChanged(bool value) => RebuildVisible();

    partial void OnShowErrorChanged(bool value) => RebuildVisible();

    partial void OnShowFatalChanged(bool value) => RebuildVisible();

    partial void OnShowUnknownChanged(bool value) => RebuildVisible();

    partial void OnSelectedEntryChanged(LogEntry? value) => HasSelection = value is not null;

    private bool IsLevelEnabled(LogLevel level) => level switch
    {
        LogLevel.Trace => ShowTrace,
        LogLevel.Debug => ShowDebug,
        LogLevel.Info => ShowInfo,
        LogLevel.Warn => ShowWarn,
        LogLevel.Error => ShowError,
        LogLevel.Fatal => ShowFatal,
        _ => ShowUnknown,
    };

    private bool IsFilterTrivial =>
        ShowTrace && ShowDebug && ShowInfo && ShowWarn && ShowError && ShowFatal && ShowUnknown
        && string.IsNullOrWhiteSpace(SearchText);

    private bool Matches(LogEntry entry)
    {
        if (!IsLevelEnabled(entry.Level))
        {
            return false;
        }

        var keyword = SearchText?.Trim();
        if (string.IsNullOrEmpty(keyword))
        {
            return true;
        }

        var comparison = CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        // 属性过滤：搜索框写成 `CustomerId=42`（左侧必须是已知属性名）时按结构化属性精确过滤。
        if (_propertyFilterActive)
        {
            if (!entry.Properties.TryGetValue(_propertyFilterName!, out var propertyValue))
            {
                return false;
            }

            return _propertyFilterValue.Length == 0
                   || propertyValue.Contains(_propertyFilterValue, comparison);
        }

        if (UseRegex)
        {
            if (_activeRegex is null)
            {
                // 正则有语法错误时不过滤，保留全部结果并把错误显示在状态栏。
                return !_regexFailed;
            }

            try
            {
                return _activeRegex.IsMatch(entry.Message)
                       || _activeRegex.IsMatch(entry.RawLine)
                       || (entry.Module is not null && _activeRegex.IsMatch(entry.Module))
                       || (entry.Exception is not null && _activeRegex.IsMatch(entry.Exception))
                       || (entry.Template is not null && _activeRegex.IsMatch(entry.Template))
                       || MatchesPropertyByRegex(entry);
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }

        return entry.Message.Contains(keyword, comparison)
               || entry.RawLine.Contains(keyword, comparison)
               || entry.SourceName.Contains(keyword, comparison)
               || (entry.Module?.Contains(keyword, comparison) ?? false)
               || (entry.Thread?.Contains(keyword, comparison) ?? false)
               || (entry.Exception?.Contains(keyword, comparison) ?? false)
               || (entry.Template?.Contains(keyword, comparison) ?? false)
               || MatchesProperty(entry, keyword, comparison);
    }

    private bool MatchesProperty(LogEntry entry, string keyword, StringComparison comparison)
    {
        if (!entry.HasProperties)
        {
            return false;
        }

        foreach (var property in entry.PropertyList)
        {
            if (property.Name.Contains(keyword, comparison) || property.Value.Contains(keyword, comparison))
            {
                return true;
            }
        }

        return false;
    }

    private bool MatchesPropertyByRegex(LogEntry entry)
    {
        if (!entry.HasProperties)
        {
            return false;
        }

        foreach (var property in entry.PropertyList)
        {
            if (_activeRegex!.IsMatch(property.Name) || _activeRegex.IsMatch(property.Value))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>解析 <c>name=value</c> 形式的属性过滤条件（name 必须是已知属性名）。</summary>
    private void PreparePropertyFilter()
    {
        _propertyFilterActive = false;
        _propertyFilterName = null;
        _propertyFilterValue = string.Empty;

        var keyword = SearchText?.Trim();
        if (string.IsNullOrEmpty(keyword))
        {
            return;
        }

        var separator = keyword.IndexOf('=');
        if (separator <= 0)
        {
            return;
        }

        var name = keyword[..separator].Trim();
        if (name.Length == 0 || name.Length > 64)
        {
            return;
        }

        if (!name.All(c => char.IsLetterOrDigit(c) || c is '_' or '.' or '-'))
        {
            return;
        }

        if (!_propertyNames.Contains(name))
        {
            return;
        }

        _propertyFilterActive = true;
        _propertyFilterName = name;
        _propertyFilterValue = keyword[(separator + 1)..].Trim();
    }

    private void RebuildVisible()
    {
        PrepareRegex();
        PreparePropertyFilter();

        var list = new List<LogEntry>(_entries.Count);
        foreach (var entry in _entries)
        {
            if (Matches(entry))
            {
                list.Add(entry);
            }
        }

        VisibleEntries.ReplaceAll(list);
        VisibleCount = list.Count;
        HasVisibleEntries = list.Count > 0;
        UpdateStatus();

        if (AutoScroll && IsFollowing)
        {
            ScrollToEndRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void PrepareRegex()
    {
        _regexFailed = false;
        _activeRegex = null;

        var keyword = SearchText?.Trim();
        if (!UseRegex || string.IsNullOrEmpty(keyword))
        {
            return;
        }

        try
        {
            _activeRegex = new Regex(
                keyword,
                (CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase) | RegexOptions.Compiled,
                TimeSpan.FromSeconds(1));
        }
        catch (ArgumentException ex)
        {
            _regexFailed = true;
            StatusText = Strings.Format("MSG_InvalidRegex", ex.Message);
        }
    }

    // ---------------------------------------------------------------- 数据追加

    private void AppendEntries(IReadOnlyList<LogEntry> entries)
    {
        foreach (var entry in entries)
        {
            _entries.Add(entry);
            CountLevel(entry.Level, 1);

            foreach (var property in entry.PropertyList)
            {
                _propertyNames.Add(property.Name);
            }
        }

        TotalCount = _entries.Count;

        PrepareRegex();
        PreparePropertyFilter();

        var appended = IsFilterTrivial
            ? entries
            : entries.Where(Matches).ToList();

        if (appended.Count > 0)
        {
            VisibleEntries.AppendRange(appended);
            VisibleCount += appended.Count;
            HasVisibleEntries = true;
        }

        UpdateStatus();

        if (AutoScroll && (IsFollowing || appended.Count > 0))
        {
            ScrollToEndRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RecalculateCounts()
    {
        TraceCount = 0;
        DebugCount = 0;
        InfoCount = 0;
        WarnCount = 0;
        ErrorCount = 0;
        FatalCount = 0;
        UnknownCount = 0;

        foreach (var entry in _entries)
        {
            CountLevel(entry.Level, 1);
        }

        TotalCount = _entries.Count;
    }

    private void CountLevel(LogLevel level, int delta)
    {
        switch (level)
        {
            case LogLevel.Trace:
                TraceCount += delta;
                break;
            case LogLevel.Debug:
                DebugCount += delta;
                break;
            case LogLevel.Info:
                InfoCount += delta;
                break;
            case LogLevel.Warn:
                WarnCount += delta;
                break;
            case LogLevel.Error:
                ErrorCount += delta;
                break;
            case LogLevel.Fatal:
                FatalCount += delta;
                break;
            default:
                UnknownCount += delta;
                break;
        }
    }

    // ---------------------------------------------------------------- 实时跟踪

    partial void OnIsFollowingChanged(bool value)
    {
        foreach (var source in Sources)
        {
            source.IsFollowing = value;
        }

        if (value)
        {
            _tailTimer.Start();
        }
        else
        {
            _tailTimer.Stop();
        }

        UpdateStatus();
    }

    private async void OnTailTick(object? sender, EventArgs e)
    {
        if (_tailBusy)
        {
            return;
        }

        _tailBusy = true;

        try
        {
            foreach (var source in Sources.Where(s => s.IsFollowing && s.Parser is not null).ToList())
            {
                if (!File.Exists(source.FilePath))
                {
                    continue;
                }

                var size = LogLoadService.SafeFileSize(source.FilePath);
                source.FileSize = size;

                if (size == source.Offset)
                {
                    continue;
                }

                if (size < source.Offset)
                {
                    // 日志轮转 / 文件被截断：从头重新读取该文件。
                    source.Offset = 0;
                    source.LineNumber = 0;
                }

                var parser = source.Parser!;
                var batch = await Task.Run(() => LogLoadService.ReadIncrement(source, parser, source.DateHint));

                if (batch.Entries.Count > 0)
                {
                    AppendEntries(batch.Entries);
                    source.EntryCount += batch.Entries.Count;
                }
            }
        }
        catch (IOException)
        {
            // 文件正在被写入/占用，下个周期再试。
        }
        catch (UnauthorizedAccessException)
        {
            IsFollowing = false;
        }
        finally
        {
            _tailBusy = false;
        }
    }

    // ---------------------------------------------------------------- 导出 / 复制

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (VisibleEntries.Count == 0)
        {
            StatusText = Strings.Get("MSG_NothingToExport");
            return;
        }

        var topLevel = GetTopLevel();
        if (topLevel is null)
        {
            return;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Strings.BTN_Export,
            SuggestedFileName = $"logs-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            DefaultExtension = "csv",
            FileTypeChoices = [s_csvFileType],
            ShowOverwritePrompt = true,
        });

        var path = file?.TryGetLocalPath();
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            var snapshot = VisibleEntries.ToList();
            await CsvExporter.ExportAsync(path, snapshot);
            StatusText = Strings.Format("MSG_ExportDone", snapshot.Count, Path.GetFileName(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText = Strings.Format("MSG_ExportFailed", ex.Message);
        }
    }

    [RelayCommand]
    private void CopySelected()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        CopyTextToClipboard(FormatEntries([SelectedEntry]));
    }

    /// <summary>由页面 code-behind 在复制多选行时回调，统一走状态栏提示。</summary>
    public void ReportCopied(int count)
    {
        if (count > 0)
        {
            StatusText = Strings.Format("MSG_Copied", count);
        }
    }

    /// <summary>把多行按「时间 级别 来源 消息」拼成可粘贴文本。</summary>
    public string FormatEntries(IEnumerable<LogEntry> entries)
    {
        var builder = new System.Text.StringBuilder();

        foreach (var entry in entries)
        {
            builder
                .Append(entry.TimeDisplay)
                .Append(" [").Append(LogLevelText.ToShortText(entry.Level)).Append("] ")
                .Append(entry.SourceName)
                .Append(" : ")
                .AppendLine(entry.Message);

            if (entry.HasException)
            {
                builder.AppendLine(entry.Exception);
            }
        }

        return builder.ToString();
    }

    public void CopyTextToClipboard(string text)
    {
        var clipboard = GetTopLevel()?.Clipboard;
        if (clipboard is null || string.IsNullOrEmpty(text))
        {
            return;
        }

        _ = clipboard.SetTextAsync(text);
    }

    [RelayCommand]
    private void OpenInExplorer(LogSource? source)
    {
        var path = source?.FilePath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            StatusText = Strings.Format("MSG_FileMissing", path ?? string.Empty);
            return;
        }

        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo = OperatingSystem.IsWindows()
                ? new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
                : new System.Diagnostics.ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open",
                    OperatingSystem.IsMacOS() ? $"-R \"{path}\"" : $"\"{Path.GetDirectoryName(path)}\"");
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            StatusText = Strings.Format("MSG_OpenFailed", ex.Message);
        }
    }

    // ---------------------------------------------------------------- 状态

    private void OnSourcesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HasSources = Sources.Count > 0;
    }

    private void BeginBusy()
    {
        _busyCounter++;
        IsBusy = true;
        StatusText = Strings.STATUS_Loading;
    }

    private void EndBusy()
    {
        _busyCounter = Math.Max(0, _busyCounter - 1);
        IsBusy = _busyCounter > 0;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (IsBusy)
        {
            StatusText = Strings.STATUS_Loading;
            return;
        }

        if (_entries.Count == 0)
        {
            StatusText = Strings.STATUS_Ready;
            return;
        }

        var text = VisibleCount == TotalCount
            ? Strings.Format("STATUS_Loaded", TotalCount)
            : Strings.Format("STATUS_Filtered", VisibleCount, TotalCount);

        if (IsFollowing)
        {
            text = text + " · " + Strings.Get("STATUS_Following");
        }

        StatusText = text;
    }

    private static DateTimeOffset SafeLastWrite(string path)
    {
        try
        {
            return File.GetLastWriteTime(path);
        }
        catch (IOException)
        {
            return DateTimeOffset.Now;
        }
    }

    private static TopLevel? GetTopLevel()
        => Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

    public override void Dispose()
    {
        _searchDebounce.Stop();
        _tailTimer.Stop();
        Sources.CollectionChanged -= OnSourcesChanged;
        base.Dispose();
    }
}

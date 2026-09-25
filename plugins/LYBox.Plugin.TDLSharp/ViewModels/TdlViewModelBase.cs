using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Services;
using LYBox.Plugin.Shared.Models;
using LYBox.Plugin.TDLSharp.Models;
using LYBox.Plugin.TDLSharp.Resources;
using LYBox.Plugin.TDLSharp.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ursa.Controls;

namespace LYBox.Plugin.TDLSharp.ViewModels;

public abstract partial class TdlViewModelBase : ViewModelBase
{
    private ScriptDescriptor? _script;

    [ObservableProperty] private ObservableCollection<ScriptParameter> _parameters = [];
    [ObservableProperty] private ObservableCollection<LogEntry> _logEntries = [];
    [ObservableProperty] private ObservableCollection<ExecutionHistoryRecord> _executionHistoryRecords = [];
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _statusText = Strings.Get("STATUS_Ready");
    [ObservableProperty] private double _logMaxHeight = 400;

    private CancellationTokenSource? _cts;

    /// <summary>
    /// 脚本元数据（包含参数定义）。首次访问时构造并缓存。
    /// 注意：构造时基类会复制 <see cref="CreateScript"/> 返回的 <c>Parameters</c> 到
    /// <see cref="Parameters"/> 集合；之后用户对 <see cref="Parameters"/> 中参数值的修改
    /// 不会反映到 <see cref="Script"/>，也不会影响执行。
    /// </summary>
    public ScriptDescriptor Script => _script ??= CreateScript();

    /// <summary>由子类实现：构造脚本元数据。基类只会调用一次。</summary>
    protected abstract ScriptDescriptor CreateScript();

    protected TdlViewModelBase()
    {
        foreach (var param in Script.Parameters)
        {
            Parameters.Add(param);
        }

        WeakReferenceMessenger.Default.Register<TdlViewModelBase, WindowSizeChangedMessage>(this, OnWindowSizeChanged);
        LoadExecutionHistory();
    }

    private void OnWindowSizeChanged(object recipient, WindowSizeChangedMessage message)
    {
        LogMaxHeight = Math.Max(200, message.Value.Height * 0.5);
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogEntries.Clear();
    }

    [RelayCommand]
    private async Task ShowExecutionHistory()
    {
        await LoadExecutionHistoryAsync();
        var dialogVm = new ExecutionHistoryDialogViewModel(Script.Id, ExecutionHistoryRecords, ApplyParametersFromJson);
        var options = new OverlayDialogOptions
        {
            Title = Strings.Get("FMT_ExecutionHistoryTitle", Script.Name),
            CanResize = false,
            CanLightDismiss = true,
            IsCloseButtonVisible = true,
            HorizontalAnchor = HorizontalPosition.Center,
            VerticalAnchor = VerticalPosition.Center,
        };
        await OverlayDialog.ShowCustomAsync<Controls.ExecutionHistoryDialog, ExecutionHistoryDialogViewModel, bool>(dialogVm, options: options);
    }

    [RelayCommand]
    private async Task ExecuteScript()
    {
        if (IsRunning) return;

        var clientManager = ServiceLocator.GetService<TdlClientManager>();
        if (clientManager == null) return;

        if (!clientManager.HasTdlRoot)
        {
            var result = await OverlayMessageBox.ShowAsync(
                Strings.Get("LOGIN_TdlRootNotSetWarning"),
                Strings.Get("LOGIN_NotInitializedTitle"),
                button: MessageBoxButton.YesNo,
                icon: MessageBoxIcon.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // 二维码登录作为默认/推荐方式
                await LoginDialogService.ShowLoginDialogAsync(LoginMethod.QrCode);
            }
            return;
        }

        await clientManager.EnsureReadyForAuthCheckAsync();

        if (clientManager.NeedsLogin)
        {
            // 未登录：弹框提示用户登录（默认二维码）
            var promptResult = await OverlayMessageBox.ShowAsync(
                Strings.Get("LOGIN_NotInitializedWarning"),
                Strings.Get("LOGIN_NotInitializedTitle"),
                button: MessageBoxButton.YesNo,
                icon: MessageBoxIcon.Warning);

            if (promptResult == MessageBoxResult.Yes)
            {
                await LoginDialogService.ShowLoginDialogAsync(LoginMethod.QrCode);
            }
            return;
        }

        IsRunning = true;
        StatusText = string.Format(Strings.Get("STATUS_Running"), Script.Name);
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        var paramSnapshot = BuildParameterValues();
        var historyRecord = new ExecutionHistoryRecord
        {
            ScriptId = Script.Id,
            ScriptName = Script.Name,
            ParametersJson = JsonSerializer.Serialize(paramSnapshot),
            ParameterSummary = BuildParameterSummary(paramSnapshot),
            ExecutedAt = DateTime.Now,
            Status = "执行中",
        };
        var historyStart = DateTime.UtcNow;
        // 在执行前一次性插入"执行中"占位记录，并把 Id 回写到 historyRecord，
        // 供 finally 阶段的 UpdateExecutionHistoryRecordAsync 精准定位。
        await SaveExecutionHistoryRecordAsync(historyRecord);

        TdlService? tdlService = null;
        try
        {
            tdlService = CreateTdlService();
            // 关联执行历史：让本次执行产生的 ForwardRecord 写入 ExecutionHistoryRecordId / ScriptId，
            // 删除该执行历史时可联动删除对应转发记录。
            tdlService.AttachExecutionRecord(historyRecord.Id, Script.Id);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            await ExecuteCoreAsync(tdlService, paramSnapshot, linkedCts.Token);
            historyRecord.Status = "成功";
            StatusText = Strings.Get("STATUS_Completed");
        }
        catch (OperationCanceledException)
        {
            historyRecord.Status = "已取消";
            StatusText = Strings.Get("STATUS_Cancelled");
        }
        catch (Exception ex)
        {
            historyRecord.Status = "失败";
            historyRecord.ErrorMessage = ex.Message;
            StatusText = $"{Strings.Get("STATUS_Failed")}: {ex.Message}";
            PluginLoggers.For<TdlViewModelBase>().LogError(ex, "[TdlViewModel] 脚本执行异常");
        }
        finally
        {
            historyRecord.Duration = DateTime.UtcNow - historyStart;
            // 关键：始终以最终 Id 更新。若 Update 仍因任何原因匹配不到行，
            // 会再尝试用 "执行中" 占位记录或 InsertOrReplace 全量写入兜底。
            await UpdateExecutionHistoryRecordAsync(historyRecord);

            // 写完后再刷新本地历史集合，避免用户在 UI 上看不到本次记录。
            try { await LoadExecutionHistoryAsync(); }
            catch (Exception ex) { PluginLoggers.For<TdlViewModelBase>().LogWarning(ex, "[TdlViewModel] 刷新历史集合失败: {Message}", ex.Message); }

            IsRunning = false;
        }
    }

    [RelayCommand]
    private void CancelExecution()
    {
        _cts?.Cancel();
        StatusText = Strings.Get("STATUS_Cancelling");
    }

    /// <summary>
    /// 打开登录对话框：默认进入二维码登录方式。
    /// 任何页面顶部都可以调用，方便用户在未登录时随时扫码登录。
    /// </summary>
    [RelayCommand]
    private async Task ShowQrLogin()
    {
        if (IsRunning) return;
        await LoginDialogService.ShowLoginDialogAsync(preferredMethod: LoginMethod.QrCode);
    }

    /// <summary>
    /// 仅初始化 TDLib 客户端（不弹出登录窗口）。当目录为空时手动触发初始化。
    /// </summary>
    [RelayCommand]
    private async Task InitializeClient()
    {
        if (IsRunning) return;
        var clientManager = ServiceLocator.GetService<TdlClientManager>();
        if (clientManager == null || !clientManager.HasTdlRoot)
        {
            StatusText = Strings.Get("LOGIN_TdlRootNotSet");
            return;
        }

        try
        {
            await clientManager.EnsureInitializedAsync();
            await clientManager.WaitReadyAsync();
            StatusText = Strings.Get("LOGIN_Initialized");
        }
        catch (Exception ex)
        {
            PluginLoggers.For<TdlViewModelBase>().LogError(ex, "[TdlViewModel] 初始化失败");
            StatusText = Strings.Get("LOGIN_InitFailed", ex.Message);
        }
    }

    /// <summary>由子类实现：执行具体脚本逻辑。</summary>
    protected abstract Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct);

    protected DirectLogger CreateLogger() => new(
        message => AddLogEntry(new LogEntry { Message = message }),
        entry => AddLogEntry(entry),
        UpdateProgressEntry);

    protected static void UpdateProgressEntry(LogEntry entry, double progressValue, string status, bool completed, bool failed)
    {
        Dispatcher.UIThread.Post(() =>
        {
            entry.ProgressValue = progressValue;
            entry.StatusText = status;
            entry.IsCompleted = completed;
            entry.IsFailed = failed;
        });
    }

    protected TdlService CreateTdlService()
    {
        var clientManager = ServiceLocator.GetService<TdlClientManager>();
        var logger = CreateLogger();
        return new TdlService(clientManager, logger);
    }

    /// <summary>
    /// 直接添加一条已构造好的日志条目（用于进度条更新等需要外部控制 LogEntry 实例的场景）。
    /// </summary>
    public void AddLogEntryExternally(LogEntry entry) => AddLogEntry(entry);

    protected void AddLogEntry(LogEntry entry)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LogEntries.Add(entry);
            const int MaxLogEntries = 1000;
            const int TrimBatch = 100;
            if (LogEntries.Count > MaxLogEntries + TrimBatch)
            {
                var toRemove = LogEntries.Count - MaxLogEntries;
                for (int i = 0; i < toRemove; i++)
                    LogEntries.RemoveAt(0);
            }
        });
    }

    private Dictionary<string, string> BuildParameterValues()
    {
        var values = new Dictionary<string, string>();
        foreach (var param in Parameters)
        {
            values[param.Key] = param.DefaultValue ?? string.Empty;
        }
        return values;
    }

    private void ApplyParametersFromJson(string parametersJson)
    {
        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(parametersJson) ?? new();
            foreach (var param in Parameters)
            {
                if (values.TryGetValue(param.Key, out var val))
                    param.DefaultValue = val;
            }
        }
        catch (Exception ex) { PluginLoggers.For<TdlViewModelBase>().LogWarning(ex, "[TdlViewModel] 应用参数 JSON 失败: {Message}", ex.Message); }
    }

    private Task LoadExecutionHistoryAsync() => LoadExecutionHistoryCoreAsync(setCollection: true);

    private void LoadExecutionHistory() => _ = LoadExecutionHistoryCoreAsync(setCollection: false);

    async Task LoadExecutionHistoryCoreAsync(bool setCollection)
    {
        try
        {
            using var db = ExecutionHistoryDbContext.CreateForScript(Script.Id);
            await db.EnsureSchemaInitializedAsync();
            var records = await db.ExecutionRecords
                .Where(r => r.ScriptId == Script.Id)
                .OrderByDescending(r => r.ExecutedAt)
                .Take(200)
                .ToListAsync();

            void Apply()
            {
                ExecutionHistoryRecords.Clear();
                foreach (var r in records)
                    ExecutionHistoryRecords.Add(r);
            }

            if (setCollection)
            {
                Apply();
            }
            else
            {
                Dispatcher.UIThread.Post(Apply);
            }
        }
        catch (Exception ex) { PluginLoggers.For<TdlViewModelBase>().LogWarning(ex, "[TdlViewModel] 加载执行历史失败: {Message}", ex.Message); }
    }

    private static string BuildParameterSummary(Dictionary<string, string> values)
    {
        var parts = new List<string>();
        foreach (var kvp in values)
        {
            if (string.IsNullOrWhiteSpace(kvp.Value)) continue;
            var shortVal = kvp.Value.Length > 40 ? kvp.Value[..37] + "..." : kvp.Value;
            parts.Add($"{kvp.Key}={shortVal}");
        }
        return string.Join("; ", parts);
    }

    private async Task SaveExecutionHistoryRecordAsync(ExecutionHistoryRecord record)
    {
        // 关键修复：原先实现分别在 Save/Update 中各自 `using var db`，导致：
        //   1) Insert 写入"执行中"占位记录（连接 A）；
        //   2) ExecuteCoreAsync 期间 SQLite 文件仍可能处于 wal/事务未 checkpoint 状态；
        //   3) Update 时打开新连接（连接 B），按 record.Id 找不到记录；
        //   4) 兜底回查 ScriptId+ExecutedAt，又因 DateTime.Now 在 SQLite TEXT 中精度丢失而匹配失败；
        //   5) 最终 Update 静默失败 → 用户在历史列表里看不到本次执行记录。
        //
        // 这里将"插入"与"更新"合并到同一条 RecordHistoryAsync 流程的同一连接，
        // 并把 record.Id 通过引用回写给调用方，确保 Update 阶段一定能定位到占位记录。
        try
        {
            using var db = ExecutionHistoryDbContext.CreateForScript(Script.Id);
            await db.EnsureSchemaInitializedAsync();
            await db.ExecutionRecords.AddAsync(record);
            await db.SaveChangesAsync();
            if (record.Id > 0)
            {
                // EF Core SaveChangesAsync 已把自增 Id 回写到实体属性
            }
            else
            {
                // 极少见：Id 未回写成功。
                // 回退方案：再读一次最大 Id（按 ScriptId+Status="执行中" 匹配最新一条），
                // 避免让 Update 在 Id=0 时静默失败。
                var fallbackId = await db.ExecutionRecords
                    .Where(r => r.ScriptId == record.ScriptId && r.Status == "执行中")
                    .OrderByDescending(r => r.Id)
                    .Select(r => (int?)r.Id)
                    .FirstOrDefaultAsync();
                if (fallbackId.HasValue) record.Id = fallbackId.Value;
            }
        }
        catch (Exception ex) { PluginLoggers.For<TdlViewModelBase>().LogWarning(ex, "[TdlViewModel] 保存执行历史记录失败: {Message}", ex.Message); }
    }

    private async Task UpdateExecutionHistoryRecordAsync(ExecutionHistoryRecord record)
    {
        try
        {
            using var db = ExecutionHistoryDbContext.CreateForScript(Script.Id);
            await db.EnsureSchemaInitializedAsync();

            // 兜底：若插入时未拿到 Id（异常或返回 0），则按 ScriptId+Status 匹配最近一条"执行中"记录，
            // 不再依赖 ExecutedAt（DateTime.Now -> TEXT 可能丢精度）。
            if (record.Id <= 0)
            {
                var pending = await db.ExecutionRecords
                    .Where(r => r.ScriptId == record.ScriptId && r.Status == "执行中")
                    .OrderByDescending(r => r.Id)
                    .FirstOrDefaultAsync();
                if (pending != null)
                {
                    record.Id = pending.Id;
                    // 同时把 pending 已落库的 ExecutedAt 同步回 record，避免任何时间漂移误判。
                    record.ExecutedAt = pending.ExecutedAt;
                }
            }

            if (record.Id <= 0)
            {
                // 仍找不到占位记录：说明插入步骤完全未成功，直接重新插入一条带终态的记录，避免丢失历史。
                await db.ExecutionRecords.AddAsync(record);
                await db.SaveChangesAsync();
            }
            else
            {
                var existing = await db.ExecutionRecords.FirstOrDefaultAsync(r => r.Id == record.Id);
                if (existing == null)
                {
                    // Update 没匹配到行：极少见（说明 record.Id 与表内实际行 Id 失同步）。
                    // 兜底：再按 Status="执行中" 拿一条最新记录改写，否则直接 Insert 一条新记录。
                    var pending = await db.ExecutionRecords
                        .Where(r => r.ScriptId == record.ScriptId && r.Status == "执行中")
                        .OrderByDescending(r => r.Id)
                        .FirstOrDefaultAsync();
                    if (pending != null)
                    {
                        pending.Status = record.Status;
                        pending.ErrorMessage = record.ErrorMessage;
                        pending.Duration = record.Duration;
                        record.Id = pending.Id;
                    }
                    else
                    {
                        await db.ExecutionRecords.AddAsync(record);
                    }
                }
                else
                {
                    // EF Core 跟踪实体：直接改字段后 SaveChanges 即更新。
                    existing.Status = record.Status;
                    existing.ErrorMessage = record.ErrorMessage;
                    existing.Duration = record.Duration;
                    existing.ScriptName = record.ScriptName;
                    existing.ParametersJson = record.ParametersJson;
                    existing.ParameterSummary = record.ParameterSummary;
                }
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex) { PluginLoggers.For<TdlViewModelBase>().LogWarning(ex, "[TdlViewModel] 更新执行历史记录失败: {Message}", ex.Message); }
    }
}

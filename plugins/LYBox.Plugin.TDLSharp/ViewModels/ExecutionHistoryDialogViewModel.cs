using System.Collections.ObjectModel;
using System.Diagnostics;
using LYBox.Plugin.TDLSharp.Models;
using LYBox.Plugin.TDLSharp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace LYBox.Plugin.TDLSharp.ViewModels;

public partial class ExecutionHistoryDialogViewModel : ObservableObject, IDialogContext
{
    private readonly Action<string>? _applyParametersCallback;
    private readonly string _scriptId;

    [ObservableProperty] private ExecutionHistoryRecord? _selectedRecord;

    public ObservableCollection<ExecutionHistoryRecord> Records { get; }

    public ExecutionHistoryDialogViewModel(
        string scriptId,
        ObservableCollection<ExecutionHistoryRecord> records,
        Action<string>? applyParametersCallback = null)
    {
        _scriptId = scriptId;
        Records = records;
        _applyParametersCallback = applyParametersCallback;
    }

    public void Close()
    {
        RequestClose?.Invoke(this, null);
    }

    public event EventHandler<object?>? RequestClose;

    [RelayCommand]
    private async Task DeleteRecord(ExecutionHistoryRecord? record)
    {
        if (record == null) return;

        using var db = ExecutionHistoryDbContext.CreateForScript(_scriptId);
        await db.EnsureSchemaInitializedAsync();
        await db.ExecutionRecords
            .Where(r => r.Id == record.Id)
            .ExecuteDeleteAsync();

        // 联动删除该执行历史产生的全部转发记录（仅本地库，不动 Telegram）。
        try
        {
            var deletedFwd = await ForwardDbContext.DeleteByExecutionHistoryAsync(_scriptId, record.Id);
            if (deletedFwd > 0)
            {
                PluginLoggers.For<ExecutionHistoryDialogViewModel>()
                    .LogInformation("[ExecutionHistory] 删除 ExecutionRecord#{RecordId} 联动清理 {Deleted} 条转发记录", record.Id, deletedFwd);
            }
        }
        catch (Exception ex)
        {
            PluginLoggers.For<ExecutionHistoryDialogViewModel>()
                .LogWarning(ex, "[ExecutionHistory] 联动清理转发记录失败: {Message}", ex.Message);
        }

        Records.Remove(record);
    }

    [RelayCommand]
    private async Task ClearAll()
    {
        if (Records.Count == 0) return;

        // 先逐条收集待删的 ExecutionRecord.Id（避免删除过程中 Records 集合变化）。
        var ids = Records.Select(r => r.Id).ToList();

        using var db = ExecutionHistoryDbContext.CreateForScript(_scriptId);
        await db.EnsureSchemaInitializedAsync();
        await db.ExecutionRecords
            .Where(r => r.ScriptId == _scriptId)
            .ExecuteDeleteAsync();

        // 联动删除所有这些执行历史产生的转发记录。
        int totalFwdDeleted = 0;
        foreach (var id in ids)
        {
            try
            {
                totalFwdDeleted += await ForwardDbContext.DeleteByExecutionHistoryAsync(_scriptId, id);
            }
            catch (Exception ex)
            {
                PluginLoggers.For<ExecutionHistoryDialogViewModel>()
                    .LogWarning(ex, "[ExecutionHistory] 清空时联动清理 ExecutionRecord#{RecordId} 失败: {Message}", id, ex.Message);
            }
        }
        if (totalFwdDeleted > 0)
        {
            PluginLoggers.For<ExecutionHistoryDialogViewModel>()
            .LogInformation("[ExecutionHistory] 清空 ExecutionHistory 联动清理 {Deleted} 条转发记录", totalFwdDeleted);
        }

        Records.Clear();
    }

    [RelayCommand]
    private void ApplyParameters(ExecutionHistoryRecord? record)
    {
        if (record == null || _applyParametersCallback == null) return;
        _applyParametersCallback(record.ParametersJson);
        Close();
    }

    [RelayCommand]
    private void CloseDialog()
    {
        Close();
    }
}

using System.Collections.ObjectModel;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

public partial class ExecutionHistoryDialogViewModel : ObservableObject, IDialogContext
{
    private readonly Action<string>? _applyParametersCallback;

    [ObservableProperty] private ExecutionHistoryRecord? _selectedRecord;

    public ObservableCollection<ExecutionHistoryRecord> Records { get; }

    /// <summary>
    /// 根据脚本参数定义的动态列配置
    /// </summary>
    public List<HistoryColumnDefinition> ColumnDefinitions { get; }

    public ExecutionHistoryDialogViewModel(
        ObservableCollection<ExecutionHistoryRecord> records,
        Action<string>? applyParametersCallback = null,
        List<ScriptParameter>? scriptParameters = null)
    {
        Records = records;
        _applyParametersCallback = applyParametersCallback;

        // 根据脚本参数构建列定义
        ColumnDefinitions = BuildColumnDefinitions(scriptParameters ?? []);
    }

    private static List<HistoryColumnDefinition> BuildColumnDefinitions(List<ScriptParameter> parameters)
    {
        var columns = new List<HistoryColumnDefinition>();

        // 状态图标列
        columns.Add(new HistoryColumnDefinition("StatusIcon", "", "36", isParameter: false));

        // 每个脚本参数一列
        foreach (var param in parameters)
        {
            var width = param.Key is "source" or "channel" or "target" or "output" ? "*" : "Auto";
            columns.Add(new HistoryColumnDefinition(param.Key, param.DisplayName, width, isParameter: true));
        }

        // 如果没有参数定义，回退到 ParameterSummary 列
        if (parameters.Count == 0)
        {
            columns.Add(new HistoryColumnDefinition("ParameterSummary", "参数", "*", isParameter: false));
        }

        // 固定列：执行时间、耗时、状态、错误信息
        columns.Add(new HistoryColumnDefinition("ExecutedAt", "执行时间", "150", isParameter: false));
        columns.Add(new HistoryColumnDefinition("DurationText", "耗时", "70", isParameter: false));
        columns.Add(new HistoryColumnDefinition("Status", "状态", "60", isParameter: false));
        columns.Add(new HistoryColumnDefinition("ErrorMessage", "错误信息", "140", isParameter: false));

        return columns;
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

        using var db = TdlViewModelBase.CreateExecutionHistoryDbContext();
        var existing = await db.ExecutionRecords.FindAsync(record.Id);
        if (existing != null)
        {
            db.ExecutionRecords.Remove(existing);
            await db.SaveChangesAsync();
        }

        Records.Remove(record);
    }

    [RelayCommand]
    private async Task ClearAll()
    {
        if (Records.Count == 0) return;

        var scriptId = Records[0].ScriptId;

        using var db = TdlViewModelBase.CreateExecutionHistoryDbContext();
        await db.ExecutionRecords
            .Where(r => r.ScriptId == scriptId)
            .ExecuteDeleteAsync();

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

/// <summary>
/// 历史弹框 DataGrid 列定义
/// </summary>
public class HistoryColumnDefinition
{
    public string BindingKey { get; }
    public string Header { get; }
    public string Width { get; }
    public bool IsParameter { get; }

    public HistoryColumnDefinition(string bindingKey, string header, string width, bool isParameter)
    {
        BindingKey = bindingKey;
        Header = header;
        Width = width;
        IsParameter = isParameter;
    }
}

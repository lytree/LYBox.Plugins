using System.Collections.ObjectModel;
using LYBox.Plugin.TDLSharp.Models;
using LYBox.Plugin.TDLSharp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;
using LinqToDB;
using LinqToDB.Data;

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

        using var db = ExecutionHistoryDb.CreateForScript(_scriptId);
        await db.EnsureSchemaInitializedAsync();
        await db.ExecutionRecords
            .Where(r => r.Id == record.Id)
            .DeleteAsync();

        Records.Remove(record);
    }

    [RelayCommand]
    private async Task ClearAll()
    {
        if (Records.Count == 0) return;

        using var db = ExecutionHistoryDb.CreateForScript(_scriptId);
        await db.EnsureSchemaInitializedAsync();
        await db.ExecutionRecords
            .Where(r => r.ScriptId == _scriptId)
            .DeleteAsync();

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

using System.Collections.ObjectModel;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

public abstract partial class TdlViewModelBase : ViewModelBase
{
    public abstract ScriptDescriptor Script { get; }

    [ObservableProperty] private ObservableCollection<ScriptParameter> _parameters = [];
    [ObservableProperty] private ObservableCollection<string> _logEntries = [];
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _statusText = "就绪";

    private CancellationTokenSource? _cts;

    protected TdlViewModelBase()
    {
        foreach (var param in Script.Parameters)
        {
            Parameters.Add(param);
        }
    }

    [RelayCommand]
    private async Task ExecuteScript()
    {
        if (IsRunning) return;

        IsRunning = true;
        StatusText = $"正在执行: {Script.Name}...";
        _cts = new CancellationTokenSource();

        try
        {
            var paramValues = BuildParameterValues();
            var tdlService = CreateTdlService();

            await ExecuteCoreAsync(tdlService, paramValues, _cts.Token);
            StatusText = "执行完成";
        }
        catch (OperationCanceledException)
        {
            StatusText = "已取消";
        }
        catch (Exception ex)
        {
            AddLog($"执行失败: {ex.Message}");
            StatusText = "执行失败";
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
            OnExecutionFinished();
        }
    }

    [RelayCommand]
    private void CancelExecution()
    {
        _cts?.Cancel();
        StatusText = "正在取消...";
    }

    protected abstract Task ExecuteCoreAsync(TdlService tdlService, Dictionary<string, string> paramValues, CancellationToken ct);

    protected virtual void OnExecutionFinished() { }

    protected DirectUiLogger CreateUiLogger()
    {
        return new DirectUiLogger(message => AddLog(message));
    }

    protected TdlService CreateTdlService()
    {
        var clientManager = ServiceLocator.GetService<TdlClientManager>();
        var logger = CreateUiLogger();
        return new TdlService(clientManager, logger);
    }

    protected Dictionary<string, string> BuildParameterValues()
    {
        var values = new Dictionary<string, string>();
        foreach (var param in Parameters)
        {
            values[param.Key] = param.DefaultValue ?? string.Empty;
        }
        return values;
    }

    protected void AddLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        Dispatcher.UIThread.Post(() =>
        {
            LogEntries.Add(line);
            if (LogEntries.Count > 1000)
            {
                LogEntries.RemoveAt(0);
            }
        });
    }
}

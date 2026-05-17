using System.Collections.ObjectModel;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using LogEntry = Avalonia.Plugin.TDLSharp.Models.LogEntry;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

public abstract partial class TdlViewModelBase : ViewModelBase
{
    private readonly UiLoggerProvider _loggerProvider;
    private readonly ILoggerFactory _loggerFactory;
    private TdlClientManager? _clientManager;

    public abstract ScriptDescriptor Script { get; }

    [ObservableProperty] private ObservableCollection<ScriptParameter> _parameters = [];
    [ObservableProperty] private ObservableCollection<LogEntry> _logEntries = [];
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _statusText = "就绪";

    private CancellationTokenSource? _cts;

    protected TdlViewModelBase()
    {
        _loggerProvider = new UiLoggerProvider(AddLogEntry);
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(_loggerProvider);
        });

        foreach (var param in Script.Parameters)
        {
            Parameters.Add(param);
        }
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogEntries.Clear();
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

            if (_clientManager == null)
            {
                var apiId = Environment.GetEnvironmentVariable("tdl_api_id", EnvironmentVariableTarget.User) ?? "";
                var apiHash = Environment.GetEnvironmentVariable("tdl_api_hash", EnvironmentVariableTarget.User) ?? "";
                _clientManager = new TdlClientManager(
                    _loggerFactory.CreateLogger<TdlClientManager>(),
                    apiId, apiHash);
            }

            await RunScriptAsync(Script.Id, paramValues, _cts.Token);
            StatusText = "执行完成";
        }
        catch (OperationCanceledException)
        {
            StatusText = "已取消";
        }
        catch (Exception ex)
        {
            AddLogEntry(new LogEntry { Message = $"执行失败: {ex.Message}", Level = Models.LogLevel.Error });
            StatusText = "执行失败";
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void CancelExecution()
    {
        _cts?.Cancel();
        StatusText = "正在取消...";
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

    private async Task RunScriptAsync(string scriptId, Dictionary<string, string> paramValues, CancellationToken ct)
    {
        switch (scriptId)
        {
            case "batch-forward":
            {
                var service = new TdlBatchForwardService(_clientManager!, _loggerFactory.CreateLogger<TdlBatchForwardService>());
                await service.ExecuteAsync(
                    paramValues.GetValueOrDefault("source", ""),
                    paramValues.GetValueOrDefault("sourceId"),
                    paramValues.GetValueOrDefault("target", ""),
                    bool.TryParse(paramValues.GetValueOrDefault("older", "true"), out var older) && older,
                    int.TryParse(paramValues.GetValueOrDefault("limit", "0"), out var limit) ? limit : 0,
                    bool.TryParse(paramValues.GetValueOrDefault("comments", "true"), out var comments) && comments,
                    ct);
                break;
            }
            case "clear-message":
            {
                var service = new TdlClearMessageService(_clientManager!, _loggerFactory.CreateLogger<TdlClearMessageService>());
                await service.ExecuteAsync(
                    paramValues.GetValueOrDefault("channel"),
                    paramValues.GetValueOrDefault("contains", ""),
                    bool.TryParse(paramValues.GetValueOrDefault("silent", "false"), out var silent) && silent,
                    int.TryParse(paramValues.GetValueOrDefault("limit", "0"), out var climit) ? climit : 0,
                    ct);
                break;
            }
            case "forward":
            {
                var service = new TdlDeepCopyService(_clientManager!, _loggerFactory.CreateLogger<TdlDeepCopyService>());
                await service.ExecuteAsync(
                    paramValues.GetValueOrDefault("source"),
                    int.TryParse(paramValues.GetValueOrDefault("limit", "0"), out var flimit) ? flimit : 0,
                    bool.TryParse(paramValues.GetValueOrDefault("comments", "true"), out var fcomments) && fcomments,
                    ct);
                break;
            }
            case "group-media-download":
            {
                var service = new TdlGroupMediaDownloadService(_clientManager!, _loggerFactory.CreateLogger<TdlGroupMediaDownloadService>());
                await service.ExecuteAsync(
                    paramValues.GetValueOrDefault("link", ""),
                    paramValues.GetValueOrDefault("output"),
                    bool.TryParse(paramValues.GetValueOrDefault("includeComments", "true"), out var inc) && inc,
                    ct);
                break;
            }
            case "message-export":
            {
                var service = new TdlMessageExporterService(_clientManager!, _loggerFactory.CreateLogger<TdlMessageExporterService>());
                await service.ExecuteAsync(
                    paramValues.GetValueOrDefault("channel", ""),
                    paramValues.GetValueOrDefault("output"),
                    bool.TryParse(paramValues.GetValueOrDefault("comments", "false"), out var expComments) && expComments,
                    int.TryParse(paramValues.GetValueOrDefault("limit", "0"), out var elimit) ? elimit : 0,
                    ct);
                break;
            }
            default:
                AddLogEntry(new LogEntry { Message = $"未知脚本: {scriptId}", Level = Models.LogLevel.Error });
                break;
        }
    }

    private void AddLogEntry(LogEntry entry)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            LogEntries.Add(entry);
            if (LogEntries.Count > 2000)
            {
                LogEntries.RemoveAt(0);
            }
        });
    }
}

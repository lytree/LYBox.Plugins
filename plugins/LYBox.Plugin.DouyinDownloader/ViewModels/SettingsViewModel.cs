using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LYBox.Plugin.DouyinDownloader.Config;
using LYBox.Plugin.DouyinDownloader.WebServer;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;

namespace LYBox.Plugin.DouyinDownloader.ViewModels;

[NavigationItem("Douyin_Settings")]
[Menu("NAV_DouyinSettings", "Douyin_Settings", ParentKey = "NAV_DouyinRoot", Order = 4)]
[ViewMap(typeof(Pages.SettingsPage))]
public partial class SettingsViewModel : ViewModelBase
{
    private readonly DownloaderSettingsStore _store;
    private readonly WebConsoleServer _web;

    [ObservableProperty] private string _signerEndpoint = "";
    [ObservableProperty] private string _downloadPath = "";
    [ObservableProperty] private string _proxy = "";
    [ObservableProperty] private int _concurrency = 5;
    [ObservableProperty] private int _retryTimes = 3;
    [ObservableProperty] private bool _enableDatabase = true;
    [ObservableProperty] private bool _enableIncremental = true;
    [ObservableProperty] private bool _enableWebConsole = true;
    [ObservableProperty] private string _webConsoleHost = "127.0.0.1";
    [ObservableProperty] private int _webConsolePort = 8765;
    [ObservableProperty] private string _fileTemplate = "{date}_{title}_{id}";
    [ObservableProperty] private string _folderTemplate = "{date}_{title}_{id}";
    [ObservableProperty] private string _webhookUrl = "";
    [ObservableProperty] private bool _enableTranscript = false;
    [ObservableProperty] private string _transcriptApiKey = "";
    [ObservableProperty] private string _transcriptModel = "gpt-4o-mini-transcribe";
    [ObservableProperty] private string _statusText = "";

    public SettingsViewModel()
    {
        _store = ServiceLocator.TryGetService<DownloaderSettingsStore>(out var s) ? s! : throw new InvalidOperationException();
        _web = ServiceLocator.TryGetService<WebConsoleServer>(out var w) ? w! : throw new InvalidOperationException();
        var cur = _store.Current;
        SignerEndpoint = cur.SignerEndpoint;
        DownloadPath = cur.DownloadPath;
        Proxy = cur.Proxy;
        Concurrency = cur.Concurrency;
        RetryTimes = cur.RetryTimes;
        EnableDatabase = cur.Database;
        EnableIncremental = cur.EnableIncremental;
        EnableWebConsole = cur.EnableWebConsole;
        WebConsoleHost = cur.WebConsoleHost;
        WebConsolePort = cur.WebConsolePort;
        FileTemplate = cur.FileTemplate;
        FolderTemplate = cur.FolderStyle;
        WebhookUrl = cur.WebhookUrl;
        EnableTranscript = cur.EnableTranscript;
        TranscriptApiKey = cur.TranscriptApiKey;
        TranscriptModel = cur.TranscriptModel;
    }

    [RelayCommand]
    private void Save()
    {
        var cur = _store.Current;
        cur.SignerEndpoint = SignerEndpoint;
        cur.DownloadPath = DownloadPath;
        cur.Proxy = Proxy;
        cur.Concurrency = Math.Max(1, Concurrency);
        cur.RetryTimes = Math.Max(0, RetryTimes);
        cur.Database = EnableDatabase;
        cur.EnableIncremental = EnableIncremental;
        cur.EnableWebConsole = EnableWebConsole;
        cur.WebConsoleHost = WebConsoleHost;
        cur.WebConsolePort = WebConsolePort;
        cur.FileTemplate = FileTemplate;
        cur.FolderStyle = FolderTemplate;
        cur.WebhookUrl = WebhookUrl;
        cur.EnableTranscript = EnableTranscript;
        cur.TranscriptApiKey = TranscriptApiKey;
        cur.TranscriptModel = TranscriptModel;
        _store.Save();
        StatusText = "已保存。重启 Web 控制台生效: " + (EnableWebConsole ? $"http://{WebConsoleHost}:{WebConsolePort}/" : "已禁用");
    }

    [RelayCommand]
    private void OpenWebConsole()
    {
        if (!EnableWebConsole) { StatusText = "Web 控制台未启用"; return; }
        try { _web.Start(); } catch { }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = $"http://{WebConsoleHost}:{WebConsolePort}/",
                UseShellExecute = true,
            });
            StatusText = "Web 控制台已启动,已在浏览器打开";
        }
        catch (Exception ex) { StatusText = "启动失败: " + ex.Message; }
    }
}

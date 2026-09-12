using System.Text.Json;
using System.Text.Json.Serialization;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Services;

namespace LYBox.Plugin.DouyinDownloader.Config;

/// <summary>用户设置（持久化到插件数据目录的 settings.json）。</summary>
public sealed class DownloaderSettings
{
    public string DownloadPath { get; set; } = "Downloaded";
    public string SignerEndpoint { get; set; } = "";       // 第三方签名服务地址,留空则本地 XBogus
    public string Proxy { get; set; } = "";                // http://127.0.0.1:7890
    public int Concurrency { get; set; } = 5;
    public int RetryTimes { get; set; } = 3;
    public bool Database { get; set; } = true;
    public string DatabasePath { get; set; } = "dy_downloader.db";
    public bool EnableIncremental { get; set; } = true;
    public bool EnableTranscript { get; set; } = false;
    public string TranscriptApiKey { get; set; } = "";
    public string TranscriptModel { get; set; } = "gpt-4o-mini-transcribe";
    public string WebhookUrl { get; set; } = "";
    public bool EnableWebConsole { get; set; } = true;
    public string WebConsoleHost { get; set; } = "127.0.0.1";
    public int WebConsolePort { get; set; } = 8765;

    // 命名模板
    public string FileTemplate { get; set; } = "{date}_{title}_{id}";
    public string FolderStyle { get; set; } = "{date}_{title}_{id}";

    // 直播录制参数 (与原项目 live.* 对齐)
    public double LiveMaxDurationSeconds { get; set; } = 0;     // 0 = 录到主播下播
    public int LiveChunkSize { get; set; } = 65536;
    public double LiveIdleTimeoutSeconds { get; set; } = 30.0;

    // 浏览器兜底 (Playwright)
    public bool BrowserFallbackEnabled { get; set; } = true;
    public bool BrowserFallbackHeadless { get; set; } = false;
    public int BrowserFallbackMaxScrolls { get; set; } = 240;
    public int BrowserFallbackIdleRounds { get; set; } = 8;
    public int BrowserFallbackWaitTimeoutSeconds { get; set; } = 600;
}

/// <summary>设置读写器。</summary>
public sealed class DownloaderSettingsStore
{
    private readonly object _lock = new();
    private readonly string _path;
    public DownloaderSettings Current { get; private set; } = new();

    public DownloaderSettingsStore()
    {
        _path = ResolveSettingsPath();
        Load();
    }

    public void Save()
    {
        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(Current, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            }));
        }
    }

    public void Load()
    {
        lock (_lock)
        {
            if (!File.Exists(_path))
            {
                Save();
                return;
            }
            try
            {
                var json = File.ReadAllText(_path);
                var loaded = JsonSerializer.Deserialize<DownloaderSettings>(json);
                if (loaded != null) Current = loaded;
            }
            catch
            {
                // ignore corrupted config; keep defaults
            }
        }
    }

    private static string ResolveSettingsPath()
    {
        const string pid = "DouyinDownloader";
        if (ServiceLocator.TryGetService<IPluginDataDirectoryProvider>(out var provider) && provider != null)
            return Path.Combine(provider.GetSubDirectory(pid, ""), "settings.json");
        return Path.Combine(AppContext.BaseDirectory, "dy_settings.json");
    }
}

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using LYBox.Plugin.Downloader.Utils;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Services;

namespace LYBox.Plugin.Downloader.Config;

/// <summary>抖音子模块用户设置。</summary>
public sealed class DouyinSettings
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

/// <summary>
/// 抖音设置项的 <c>ISettingsService</c> 键。由插件在 <c>RegisterAsync</c> 中主动注册,
/// 宿主设置页统一渲染与持久化(SQLite)。
/// </summary>
public static class DouyinSettingKeys
{
    /// <summary>基础设置分组(宿主设置页显示名见 resx 的 GROUP_DOUYIN)。</summary>
    public const string Group = "Douyin";

    /// <summary>高级设置分组(宿主设置页显示名见 resx 的 GROUP_DOUYINADVANCED)。</summary>
    public const string AdvancedGroup = "DouyinAdvanced";

    public const string SignerEndpoint = "DL.Douyin.SignerEndpoint";
    public const string DownloadPath = "DL.Douyin.DownloadPath";
    public const string Proxy = "DL.Douyin.Proxy";
    public const string Concurrency = "DL.Douyin.Concurrency";
    public const string RetryTimes = "DL.Douyin.RetryTimes";
    public const string EnableDatabase = "DL.Douyin.EnableDatabase";
    public const string EnableIncremental = "DL.Douyin.EnableIncremental";
    public const string FileTemplate = "DL.Douyin.FileTemplate";
    public const string FolderTemplate = "DL.Douyin.FolderTemplate";
    public const string WebhookUrl = "DL.Douyin.WebhookUrl";
    public const string EnableTranscript = "DL.Douyin.EnableTranscript";
    public const string TranscriptApiKey = "DL.Douyin.TranscriptApiKey";
    public const string TranscriptModel = "DL.Douyin.TranscriptModel";
    public const string EnableWebConsole = "DL.Douyin.EnableWebConsole";
    public const string WebConsoleHost = "DL.Douyin.WebConsoleHost";
    public const string WebConsolePort = "DL.Douyin.WebConsolePort";

    public const string DatabasePath = "DL.Douyin.DatabasePath";
    public const string LiveMaxDurationSeconds = "DL.Douyin.LiveMaxDurationSeconds";
    public const string LiveChunkSize = "DL.Douyin.LiveChunkSize";
    public const string LiveIdleTimeoutSeconds = "DL.Douyin.LiveIdleTimeoutSeconds";
    public const string BrowserFallbackEnabled = "DL.Douyin.BrowserFallbackEnabled";
    public const string BrowserFallbackHeadless = "DL.Douyin.BrowserFallbackHeadless";
    public const string BrowserFallbackMaxScrolls = "DL.Douyin.BrowserFallbackMaxScrolls";
    public const string BrowserFallbackIdleRounds = "DL.Douyin.BrowserFallbackIdleRounds";
    public const string BrowserFallbackWaitTimeoutSeconds = "DL.Douyin.BrowserFallbackWaitTimeoutSeconds";
}

/// <summary>
/// 抖音子模块设置读写器。设置项由插件注册到宿主 <see cref="ISettingsService"/>,
/// 统一在宿主设置页渲染与持久化;每次 <see cref="Current"/> 都重新读取,修改即时生效。
///
/// 历史版本保存在 <c>Data/{PluginId}/douyin/settings.json</c>。首次在 <see cref="ISettingsService"/>
/// 可用时自动导入并重命名为 <c>settings.json.migrated</c>,之后 JSON 不再作为主存储。
/// <see cref="ISettingsService"/> 不可用(脱离宿主的场景)时回退到同一 JSON 文件。
/// </summary>
public sealed class DouyinSettingsStore
{
    /// <summary>历史 JSON 文件名(在抖音子目录内)。</summary>
    public const string FileName = "settings.json";

    /// <summary>导入完成后的重命名后缀,防止重复导入覆盖用户在设置页的修改。</summary>
    private const string MigratedSuffix = ".migrated";

    private static readonly DouyinSettings Defaults = new();
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _jsonPath;
    private int _migrated;

    public DouyinSettingsStore()
    {
        _jsonPath = ResolveSettingsPath();
    }

    /// <summary>当前设置(每次访问都从 ISettingsService 读取最新值,确保设置页修改立即生效)。</summary>
    public DouyinSettings Current => Load();

    public DouyinSettings Load()
    {
        if (ServiceLocator.TryGetService<ISettingsService>(out var settingsService) && settingsService is not null)
        {
            EnsureMigrated(settingsService);
            return Read(settingsService);
        }

        return ReadJson();
    }

    public void Save(DouyinSettings settings)
    {
        if (!ServiceLocator.TryGetService<ISettingsService>(out var svc) || svc is null)
        {
            WriteJson(settings);
            return;
        }

        svc.SetValue(DouyinSettingKeys.SignerEndpoint, settings.SignerEndpoint);
        svc.SetValue(DouyinSettingKeys.DownloadPath, settings.DownloadPath);
        svc.SetValue(DouyinSettingKeys.Proxy, settings.Proxy);
        svc.SetValue(DouyinSettingKeys.Concurrency, settings.Concurrency);
        svc.SetValue(DouyinSettingKeys.RetryTimes, settings.RetryTimes);
        svc.SetValue(DouyinSettingKeys.EnableDatabase, settings.Database);
        svc.SetValue(DouyinSettingKeys.EnableIncremental, settings.EnableIncremental);
        svc.SetValue(DouyinSettingKeys.FileTemplate, settings.FileTemplate);
        svc.SetValue(DouyinSettingKeys.FolderTemplate, settings.FolderStyle);
        svc.SetValue(DouyinSettingKeys.WebhookUrl, settings.WebhookUrl);
        svc.SetValue(DouyinSettingKeys.EnableTranscript, settings.EnableTranscript);
        svc.SetValue(DouyinSettingKeys.TranscriptApiKey, settings.TranscriptApiKey);
        svc.SetValue(DouyinSettingKeys.TranscriptModel, settings.TranscriptModel);
        svc.SetValue(DouyinSettingKeys.EnableWebConsole, settings.EnableWebConsole);
        svc.SetValue(DouyinSettingKeys.WebConsoleHost, settings.WebConsoleHost);
        svc.SetValue(DouyinSettingKeys.WebConsolePort, settings.WebConsolePort);
        svc.SetValue(DouyinSettingKeys.DatabasePath, settings.DatabasePath);
        svc.SetValue(DouyinSettingKeys.LiveMaxDurationSeconds,
            settings.LiveMaxDurationSeconds.ToString(CultureInfo.InvariantCulture));
        svc.SetValue(DouyinSettingKeys.LiveChunkSize, settings.LiveChunkSize);
        svc.SetValue(DouyinSettingKeys.LiveIdleTimeoutSeconds,
            settings.LiveIdleTimeoutSeconds.ToString(CultureInfo.InvariantCulture));
        svc.SetValue(DouyinSettingKeys.BrowserFallbackEnabled, settings.BrowserFallbackEnabled);
        svc.SetValue(DouyinSettingKeys.BrowserFallbackHeadless, settings.BrowserFallbackHeadless);
        svc.SetValue(DouyinSettingKeys.BrowserFallbackMaxScrolls, settings.BrowserFallbackMaxScrolls);
        svc.SetValue(DouyinSettingKeys.BrowserFallbackIdleRounds, settings.BrowserFallbackIdleRounds);
        svc.SetValue(DouyinSettingKeys.BrowserFallbackWaitTimeoutSeconds, settings.BrowserFallbackWaitTimeoutSeconds);
    }

    private static DouyinSettings Read(ISettingsService svc)
    {
        return new DouyinSettings
        {
            SignerEndpoint = svc.GetValue<string>(DouyinSettingKeys.SignerEndpoint) ?? Defaults.SignerEndpoint,
            DownloadPath = svc.GetValue<string>(DouyinSettingKeys.DownloadPath) ?? Defaults.DownloadPath,
            Proxy = svc.GetValue<string>(DouyinSettingKeys.Proxy) ?? Defaults.Proxy,
            Concurrency = ReadInt(svc, DouyinSettingKeys.Concurrency, Defaults.Concurrency),
            RetryTimes = ReadInt(svc, DouyinSettingKeys.RetryTimes, Defaults.RetryTimes),
            Database = svc.GetValue<bool>(DouyinSettingKeys.EnableDatabase),
            EnableIncremental = svc.GetValue<bool>(DouyinSettingKeys.EnableIncremental),
            FileTemplate = svc.GetValue<string>(DouyinSettingKeys.FileTemplate) ?? Defaults.FileTemplate,
            FolderStyle = svc.GetValue<string>(DouyinSettingKeys.FolderTemplate) ?? Defaults.FolderStyle,
            WebhookUrl = svc.GetValue<string>(DouyinSettingKeys.WebhookUrl) ?? Defaults.WebhookUrl,
            EnableTranscript = svc.GetValue<bool>(DouyinSettingKeys.EnableTranscript),
            TranscriptApiKey = svc.GetValue<string>(DouyinSettingKeys.TranscriptApiKey) ?? Defaults.TranscriptApiKey,
            TranscriptModel = svc.GetValue<string>(DouyinSettingKeys.TranscriptModel) ?? Defaults.TranscriptModel,
            EnableWebConsole = svc.GetValue<bool>(DouyinSettingKeys.EnableWebConsole),
            WebConsoleHost = svc.GetValue<string>(DouyinSettingKeys.WebConsoleHost) ?? Defaults.WebConsoleHost,
            WebConsolePort = ReadInt(svc, DouyinSettingKeys.WebConsolePort, Defaults.WebConsolePort),
            DatabasePath = svc.GetValue<string>(DouyinSettingKeys.DatabasePath) ?? Defaults.DatabasePath,
            LiveMaxDurationSeconds = ReadDouble(svc, DouyinSettingKeys.LiveMaxDurationSeconds, Defaults.LiveMaxDurationSeconds),
            LiveChunkSize = ReadInt(svc, DouyinSettingKeys.LiveChunkSize, Defaults.LiveChunkSize),
            LiveIdleTimeoutSeconds = ReadDouble(svc, DouyinSettingKeys.LiveIdleTimeoutSeconds, Defaults.LiveIdleTimeoutSeconds),
            BrowserFallbackEnabled = svc.GetValue<bool>(DouyinSettingKeys.BrowserFallbackEnabled),
            BrowserFallbackHeadless = svc.GetValue<bool>(DouyinSettingKeys.BrowserFallbackHeadless),
            BrowserFallbackMaxScrolls = ReadInt(svc, DouyinSettingKeys.BrowserFallbackMaxScrolls, Defaults.BrowserFallbackMaxScrolls),
            BrowserFallbackIdleRounds = ReadInt(svc, DouyinSettingKeys.BrowserFallbackIdleRounds, Defaults.BrowserFallbackIdleRounds),
            BrowserFallbackWaitTimeoutSeconds = ReadInt(svc, DouyinSettingKeys.BrowserFallbackWaitTimeoutSeconds, Defaults.BrowserFallbackWaitTimeoutSeconds),
        };
    }

    private static int ReadInt(ISettingsService svc, string key, int fallback)
        => int.TryParse(svc.GetValue<string>(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    private static double ReadDouble(ISettingsService svc, string key, double fallback)
        => double.TryParse(svc.GetValue<string>(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    /// <summary>
    /// 一次性导入历史 settings.json:仅当设置项仍是默认值(用户未在设置页改过)时写入,
    /// 成功后把 JSON 重命名为 <c>settings.json.migrated</c>,避免下次启动重复覆盖。
    /// </summary>
    private void EnsureMigrated(ISettingsService svc)
    {
        if (Volatile.Read(ref _migrated) == 1) return;

        lock (this)
        {
            if (_migrated == 1) return;

            try
            {
                if (!File.Exists(_jsonPath))
                {
                    Volatile.Write(ref _migrated, 1);
                    return;
                }

                var json = JsonSerializer.Deserialize<DouyinSettings>(File.ReadAllText(_jsonPath), JsonOpts);
                if (json is null)
                {
                    Volatile.Write(ref _migrated, 1);
                    return;
                }

                ApplyIfUnset(svc, DouyinSettingKeys.SignerEndpoint, json.SignerEndpoint);
                ApplyIfUnset(svc, DouyinSettingKeys.DownloadPath, json.DownloadPath);
                ApplyIfUnset(svc, DouyinSettingKeys.Proxy, json.Proxy);
                ApplyIfUnset(svc, DouyinSettingKeys.Concurrency, json.Concurrency);
                ApplyIfUnset(svc, DouyinSettingKeys.RetryTimes, json.RetryTimes);
                ApplyIfUnset(svc, DouyinSettingKeys.EnableDatabase, json.Database);
                ApplyIfUnset(svc, DouyinSettingKeys.EnableIncremental, json.EnableIncremental);
                ApplyIfUnset(svc, DouyinSettingKeys.FileTemplate, json.FileTemplate);
                ApplyIfUnset(svc, DouyinSettingKeys.FolderTemplate, json.FolderStyle);
                ApplyIfUnset(svc, DouyinSettingKeys.WebhookUrl, json.WebhookUrl);
                ApplyIfUnset(svc, DouyinSettingKeys.EnableTranscript, json.EnableTranscript);
                ApplyIfUnset(svc, DouyinSettingKeys.TranscriptApiKey, json.TranscriptApiKey);
                ApplyIfUnset(svc, DouyinSettingKeys.TranscriptModel, json.TranscriptModel);
                ApplyIfUnset(svc, DouyinSettingKeys.EnableWebConsole, json.EnableWebConsole);
                ApplyIfUnset(svc, DouyinSettingKeys.WebConsoleHost, json.WebConsoleHost);
                ApplyIfUnset(svc, DouyinSettingKeys.WebConsolePort, json.WebConsolePort);
                ApplyIfUnset(svc, DouyinSettingKeys.DatabasePath, json.DatabasePath);
                ApplyIfUnset(svc, DouyinSettingKeys.LiveMaxDurationSeconds,
                    json.LiveMaxDurationSeconds.ToString(CultureInfo.InvariantCulture));
                ApplyIfUnset(svc, DouyinSettingKeys.LiveChunkSize, json.LiveChunkSize);
                ApplyIfUnset(svc, DouyinSettingKeys.LiveIdleTimeoutSeconds,
                    json.LiveIdleTimeoutSeconds.ToString(CultureInfo.InvariantCulture));
                ApplyIfUnset(svc, DouyinSettingKeys.BrowserFallbackEnabled, json.BrowserFallbackEnabled);
                ApplyIfUnset(svc, DouyinSettingKeys.BrowserFallbackHeadless, json.BrowserFallbackHeadless);
                ApplyIfUnset(svc, DouyinSettingKeys.BrowserFallbackMaxScrolls, json.BrowserFallbackMaxScrolls);
                ApplyIfUnset(svc, DouyinSettingKeys.BrowserFallbackIdleRounds, json.BrowserFallbackIdleRounds);
                ApplyIfUnset(svc, DouyinSettingKeys.BrowserFallbackWaitTimeoutSeconds, json.BrowserFallbackWaitTimeoutSeconds);

                File.Move(_jsonPath, _jsonPath + MigratedSuffix, overwrite: true);
                Volatile.Write(ref _migrated, 1);
            }
            catch (Exception ex)
            {
                // 导入失败保留 JSON,下次启动重试
                Logger.Warn($"[设置迁移] 抖音 settings.json 导入失败,下次启动重试: {ex.Message}");
            }
        }
    }

    private static void ApplyIfUnset<T>(ISettingsService svc, string key, T value)
    {
        var item = svc.GetSetting(key);
        if (item is null) return;
        // 用户已在设置页改过(非空且不等于默认值)则保留用户值
        if (!string.IsNullOrEmpty(item.RawValue) && item.RawValue != item.DefaultValue) return;
        svc.SetValue(key, value);
    }

    private DouyinSettings ReadJson()
    {
        try
        {
            if (!File.Exists(_jsonPath)) return new DouyinSettings();
            var json = File.ReadAllText(_jsonPath);
            return JsonSerializer.Deserialize<DouyinSettings>(json, JsonOpts) ?? new DouyinSettings();
        }
        catch
        {
            // 忽略损坏的配置,保持默认值
            return new DouyinSettings();
        }
    }

    private void WriteJson(DouyinSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_jsonPath)!);
        File.WriteAllText(_jsonPath, JsonSerializer.Serialize(settings, JsonOpts));
    }

    /// <summary>
    /// 历史 JSON 路径:<c>Data/{PluginId}/douyin/settings.json</c>。
    /// 数据目录提供器不可用时抛 <see cref="InvalidOperationException"/>,保证 settings / cookies / db 落在同一目录。
    /// </summary>
    private static string ResolveSettingsPath()
        => Path.Combine(PluginConfigStore.ResolveDouyinDir(PluginConfigStore.CurrentProvider), FileName);
}

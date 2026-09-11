using System.Text.Json;
using LYBox.Plugin.Downloader.Models;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Services;

namespace LYBox.Plugin.Downloader.Services;

/// <summary>
/// 二进制路径与全局设置的持久化。
/// 优先通过宿主 <see cref="ISettingsService"/>（SQLite 持久化，统一在设置页管理）；
/// 若 <see cref="ServiceLocator"/> 尚未初始化（如插件早期初始化阶段），回退到本地 JSON 文件。
/// JSON 路径：主体 Data/{PluginId}/settings.json（由 <see cref="IPluginDataDirectoryProvider"/> 解析）。
/// </summary>
public static class DownloadSettingsStore
{
    /// <summary>PluginId 必须与 csproj 中的 PluginId 保持一致。</summary>
    private const string PluginId = "B2C3D4E5-F6A7-8901-BCDE-DOWNLOADER001";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// 解析 JSON 回退目录：优先经 <see cref="IPluginDataDirectoryProvider"/> 拿 Data/{PluginId}/，
    /// 不可用时回退到 %LOCALAPPDATA%/LYBox/DownloaderPlugin（兼容早期版本残留数据）。
    /// </summary>
    private static string SettingsDir
    {
        get
        {
            if (ServiceLocator.TryGetService<IPluginDataDirectoryProvider>(out var provider)
                && provider is not null)
            {
                return provider.GetPluginDataDirectory(PluginId);
            }
            // 旧版兼容路径（早期版本会把 JSON 写到这里）
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(baseDir, "LYBox", "DownloaderPlugin");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string SettingsPath => Path.Combine(SettingsDir, "settings.json");

    /// <summary>
    /// 当前设置（每次访问都从 ISettingsService 读取最新值，确保设置页修改立即生效）。
    /// 若 ServiceLocator 不可用则回退到 JSON。
    /// </summary>
    public static BinaryPaths Current => Load();

    private static ISettingsService? TryGetSettingsService()
        => ServiceLocator.TryGetService(out ISettingsService? svc) ? svc : null;

    /// <summary>从 ISettingsService 读取设置；服务不可用时回退到 JSON。</summary>
    public static BinaryPaths Load()
    {
        var svc = TryGetSettingsService();
        if (svc != null)
        {
            return new BinaryPaths
            {
                FfmpegPath = svc.GetValue<string>("DL.FfmpegPath") ?? string.Empty,
                Mp4DecryptPath = svc.GetValue<string>("DL.Mp4DecryptPath") ?? string.Empty,
                MkvmergePath = svc.GetValue<string>("DL.MkvmergePath") ?? string.Empty,
                ShakaPackagerPath = svc.GetValue<string>("DL.ShakaPackagerPath") ?? string.Empty,
                Proxy = svc.GetValue<string>("DL.Proxy"),
                UseSystemProxy = svc.GetValue<bool>("DL.UseSystemProxy"),
                LogLevel = svc.GetValue<string>("DL.LogLevel") ?? "INFO",
            };
        }
        return LoadFromJson();
    }

    /// <summary>保存设置到 ISettingsService；服务不可用时回退到 JSON。</summary>
    public static void Save(BinaryPaths cfg)
    {
        var svc = TryGetSettingsService();
        if (svc != null)
        {
            svc.SetValue("DL.FfmpegPath", cfg.FfmpegPath);
            svc.SetValue("DL.Mp4DecryptPath", cfg.Mp4DecryptPath);
            svc.SetValue("DL.MkvmergePath", cfg.MkvmergePath);
            svc.SetValue("DL.ShakaPackagerPath", cfg.ShakaPackagerPath);
            svc.SetValue("DL.Proxy", cfg.Proxy);
            svc.SetValue("DL.UseSystemProxy", cfg.UseSystemProxy);
            svc.SetValue("DL.LogLevel", cfg.LogLevel);
            return;
        }
        SaveToJson(cfg);
    }

    private static BinaryPaths LoadFromJson()
    {
        try
        {
            // 首次加载：尝试从旧版位置（%LOCALAPPDATA%/LYBox/DownloaderPlugin/settings.json）
            // 一次性迁移到主体 Data/{PluginId}/settings.json。
            MigrateLegacyFileIfNeeded();

            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var cfg = JsonSerializer.Deserialize<BinaryPaths>(json, JsonOpts);
                return cfg ?? new BinaryPaths();
            }
        }
        catch { /* 损坏文件回退默认 */ }
        return new BinaryPaths();
    }

    /// <summary>
    /// 一次性迁移：把旧版 LocalAppData 路径下的 settings.json 复制到主体 Data/{PluginId}/。
    /// 仅当目标文件不存在且旧文件存在时执行（避免覆盖新数据）。成功复制后旧文件保留以便回退。
    /// </summary>
    private static void MigrateLegacyFileIfNeeded()
    {
        try
        {
            if (File.Exists(SettingsPath)) return;
            var legacyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LYBox", "DownloaderPlugin", "settings.json");
            if (!File.Exists(legacyPath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.Copy(legacyPath, SettingsPath, overwrite: false);
        }
        catch { /* 迁移失败忽略，下次启动再尝试 */ }
    }

    private static void SaveToJson(BinaryPaths cfg)
    {
        try
        {
            var json = JsonSerializer.Serialize(cfg, JsonOpts);
            File.WriteAllText(SettingsPath, json);
        }
        catch { /* 持久化失败不阻塞 UI */ }
    }
}

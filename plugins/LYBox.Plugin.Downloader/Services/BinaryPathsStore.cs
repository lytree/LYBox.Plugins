using System.Text.Json;
using LYBox.Plugin.Downloader.Config;
using LYBox.Plugin.Downloader.Models;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Paths;
using LYBox.Plugin.Shared.Services;

namespace LYBox.Plugin.Downloader.Services;

/// <summary>
/// 「外部二进制路径 + 代理/日志级别」的持久化(模型为 <see cref="BinaryPaths"/>)。
/// 统一走宿主 <see cref="ISettingsService"/>(SQLite 持久化,设置页统一管理);
/// SettingsService 不可用视为宿主未完成初始化,直接抛 <see cref="InvalidOperationException"/>。
///
/// 历史:早期版本会把 settings.json 写到 <c>%LOCALAPPDATA%/LYBox/DownloaderPlugin/</c>;
/// 首次启动时 <see cref="EnsureMigratedLegacyFileAsync"/> 会把该文件复制到
/// <c>Data/{PluginId}/settings.json</c>(供宿主 SQLite 初始化脚本读取,完成后 SettingsService
/// 即接管,JSON 不再被读写)。
///
/// 注意区分:抖音子模块自家的下载设置由 <see cref="DouyinSettingsStore"/> 负责,
/// 落在 <c>Data/{PluginId}/douyin/settings.json</c>,两者 schema 不同、互不相干。
/// </summary>
public static class BinaryPathsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static int _migrated;

    /// <summary>
    /// 旧版 JSON 位置(早期版本会把 settings.json 写到这里):
    /// <c>%LOCALAPPDATA%/LYBox/DownloaderPlugin/settings.json</c>。
    /// 仅用于一次性迁移(<see cref="EnsureMigratedLegacyFile"/>),迁移完成后不再写入。
    /// </summary>
    public static string LegacySettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LYBox", "DownloaderPlugin", "settings.json");

    /// <summary>
    /// 主体内 JSON 路径(供迁移目标使用):<c>Data/{PluginId}/settings.json</c>。
    /// </summary>
    public static string SettingsPath
        => Path.Combine(PluginConfigStore.ResolveRootDir(PluginConfigStore.CurrentProvider),
            WellKnownPaths.PluginSettingsFileName);

    /// <summary>
    /// 当前设置(每次访问都从 ISettingsService 读取最新值,确保设置页修改立即生效)。
    /// </summary>
    public static BinaryPaths Current => Load();

    private static ISettingsService RequireSettingsService()
        => ServiceLocator.GetService<ISettingsService>()
            ?? throw new InvalidOperationException(
                "ISettingsService 未注册;BinaryPathsStore 强依赖宿主设置服务,"
                + "请确认宿主在 RegisterSettings 之前不要调用本类。");

    /// <summary>从 ISettingsService 读取设置。</summary>
    public static BinaryPaths Load()
    {
        var svc = RequireSettingsService();
        // 首次读取前尝试把旧版 LocalAppData 上的 JSON 一次性迁过来,这样用户改的设置立即反映到 SQLite。
        EnsureMigratedLegacyFile();

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

    /// <summary>保存设置到 ISettingsService。</summary>
    public static void Save(BinaryPaths cfg)
    {
        var svc = RequireSettingsService();
        svc.SetValue("DL.FfmpegPath", cfg.FfmpegPath);
        svc.SetValue("DL.Mp4DecryptPath", cfg.Mp4DecryptPath);
        svc.SetValue("DL.MkvmergePath", cfg.MkvmergePath);
        svc.SetValue("DL.ShakaPackagerPath", cfg.ShakaPackagerPath);
        svc.SetValue("DL.Proxy", cfg.Proxy);
        svc.SetValue("DL.UseSystemProxy", cfg.UseSystemProxy);
        svc.SetValue("DL.LogLevel", cfg.LogLevel);
    }

    /// <summary>
    /// 一次性迁移:把旧版 LocalAppData 路径下的 settings.json 复制到主体 Data/{PluginId}/。
    /// 仅当目标文件不存在且旧文件存在时执行(避免覆盖新数据)。成功复制后旧文件保留以便回退。
    /// 本方法对同一进程内的多次调用是幂等的(由 <c>_migrated</c> 简单标志保护)。
    /// </summary>
    public static void EnsureMigratedLegacyFile()
    {
        if (Interlocked.Exchange(ref _migrated, 1) == 1) return;
        try
        {
            if (File.Exists(SettingsPath)) return;
            if (!File.Exists(LegacySettingsPath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.Copy(LegacySettingsPath, SettingsPath, overwrite: false);
        }
        catch { /* 迁移失败忽略,下次启动再尝试 */ }
    }
}
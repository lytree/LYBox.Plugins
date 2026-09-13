using LYBox.Plugin.Downloader.Auth;
using LYBox.Plugin.Downloader.Config;
using LYBox.Plugin.Downloader.Control;
using LYBox.Plugin.Downloader.Core;
using LYBox.Plugin.Downloader.Services;
using LYBox.Plugin.Downloader.Storage;
using LYBox.Plugin.Downloader.Utils;
using LYBox.Plugin.Downloader.ViewModels;
using LYBox.Plugin.Downloader.Resources;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.Shared.Models;
using LYBox.Plugin.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LYBox.Plugin.Downloader;

[GenerateMetadata]
public partial class DownloaderPlugin
{
    public Task InitializeAsync(IServiceCollection services)
    {
        // ================ 抖音子模块（合并自 LYBox.Plugin.DouyinDownloader） ================
        // 顺序敏感：底层工具在前，依赖项引用其后。
        services.AddSingleton<PluginConfigStore>();
        services.AddSingleton<DouyinSettingsStore>();
        services.AddSingleton<RateLimiter>(sp => new RateLimiter(sp.GetRequiredService<DouyinSettingsStore>().Current.Concurrency));
        services.AddSingleton<CookieManager>();
        services.AddSingleton<SignatureClient>();
        services.AddSingleton<MediaDownloader>();
        services.AddSingleton<DownloadDatabase>();
        services.AddSingleton<DownloadQueue>();
        services.AddSingleton<DouyinApiClient>();
        services.AddSingleton<LiveSessionRegistry>();
        services.AddSingleton<DownloaderFactory>();
        services.AddSingleton<DouyinDownloadOrchestrator>();
        services.AddSingleton<DownloadCoordinator>();

        // Home 单页面聚合 4 Tab + 2 Dialog。注册 Home VM 与 5 个子 VM（Home 通过 IServiceProvider 懒加载子 VM）。
        services.AddSingleton<SubmitViewModel>();
        services.AddSingleton<JobsViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<LoginViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<DouyinHomeViewModel>();

        // ================ 原有 Downloader 设置（保留） ================
        return Task.CompletedTask;
    }

    public Task RegisterAsync(IServiceProvider serviceProvider)
    {
        // 一次性数据迁移：必须排在所有组件之前 —— DouyinSettingsStore / DownloadDatabase / CookieManager
        // 都是首次解析时按新路径读取数据目录，迁移晚于它们就会读到空目录并写出默认值。
        var dataProvider = serviceProvider.GetService<IPluginDataDirectoryProvider>()
                           ?? PluginConfigStore.CurrentProvider;
        PluginDataMigration.MigrateLegacyDouyinData(dataProvider);

        RegisterSettings(serviceProvider);

        // 主动解析抖音 DI：让 DI 异常在启动期就抛出，避免运行期点击菜单时崩溃
        _ = serviceProvider.GetRequiredService<DownloadDatabase>();

        return Task.CompletedTask;
    }

    /// <summary>将插件设置项注册到宿主设置页（ISettingsService），统一通过 SQLite 持久化</summary>
    private void RegisterSettings(IServiceProvider serviceProvider)
    {
        var settingsService = serviceProvider.GetService<ISettingsService>();
        if (settingsService == null) return;

        settingsService.RegisterSettings(
        [
            SettingDefinition.Path("DL.FfmpegPath", Strings.Get("LBL_FfmpegPath"), Strings.Get("HINT_FfmpegPath"),
                "Downloader", 10, 0, string.Empty, PluginId),
            SettingDefinition.Path("DL.Mp4DecryptPath", Strings.Get("LBL_Mp4DecryptPath"), Strings.Get("HINT_Mp4DecryptPath"),
                "Downloader", 10, 1, string.Empty, PluginId),
            SettingDefinition.Path("DL.MkvmergePath", Strings.Get("LBL_MkvmergePath"), Strings.Get("HINT_MkvmergePath"),
                "Downloader", 10, 2, string.Empty, PluginId),
            SettingDefinition.Path("DL.ShakaPackagerPath", Strings.Get("LBL_ShakaPath"), Strings.Get("HINT_ShakaPath"),
                "Downloader", 10, 3, string.Empty, PluginId),
            SettingDefinition.Text("DL.Proxy", Strings.Get("LBL_Proxy"), Strings.Get("HINT_Proxy"), Strings.Get("HINT_Proxy"),
                "Downloader", 10, 4, string.Empty, PluginId),
            SettingDefinition.Switch("DL.UseSystemProxy", Strings.Get("LBL_UseSystemProxy"), Strings.Get("DESC_UseSystemProxy"),
                "Downloader", 10, 5, true, PluginId),
            SettingDefinition.Text("DL.LogLevel", Strings.Get("LBL_LogLevel"), null, "",
                "Downloader", 10, 6, "INFO", PluginId),
        ]);
    }
}

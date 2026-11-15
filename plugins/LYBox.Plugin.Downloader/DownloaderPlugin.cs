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

        // Home 单页面聚合 4 Tab + 1 Dialog。注册 Home VM 与 4 个子 VM（Home 通过 IServiceProvider 懒加载子 VM）。
        services.AddSingleton<SubmitViewModel>();
        services.AddSingleton<JobsViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<LoginViewModel>();
        services.AddSingleton<DouyinHomeViewModel>();

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

            // ================ 抖音下载设置（原插件内自建设置弹窗，现由宿主设置页统一渲染） ================
            SettingDefinition.Text(DouyinSettingKeys.SignerEndpoint,
                Strings.Get("LBL_DouyinSignerEndpoint"), Strings.Get("DESC_DouyinSignerEndpoint"),
                "http://127.0.0.1:8888/sign", DouyinSettingKeys.Group, 11, 0, "", PluginId),
            SettingDefinition.Path(DouyinSettingKeys.DownloadPath,
                Strings.Get("LBL_DouyinDownloadPath"), Strings.Get("DESC_DouyinDownloadPath"),
                DouyinSettingKeys.Group, 11, 1, new DouyinSettings().DownloadPath, PluginId, isFolder: true),
            SettingDefinition.Text(DouyinSettingKeys.Proxy,
                Strings.Get("LBL_DouyinProxy"), Strings.Get("DESC_DouyinProxy"),
                Strings.Get("HINT_Proxy"), DouyinSettingKeys.Group, 11, 2, "", PluginId),
            SettingDefinition.Text(DouyinSettingKeys.Concurrency,
                Strings.Get("LBL_DouyinConcurrency"), Strings.Get("DESC_DouyinConcurrency"),
                "5", DouyinSettingKeys.Group, 11, 3,
                new DouyinSettings().Concurrency.ToString(), PluginId),
            SettingDefinition.Text(DouyinSettingKeys.RetryTimes,
                Strings.Get("LBL_DouyinRetryTimes"), Strings.Get("DESC_DouyinRetryTimes"),
                "3", DouyinSettingKeys.Group, 11, 4,
                new DouyinSettings().RetryTimes.ToString(), PluginId),
            SettingDefinition.Switch(DouyinSettingKeys.EnableDatabase,
                Strings.Get("LBL_DouyinEnableDatabase"), Strings.Get("DESC_DouyinEnableDatabase"),
                DouyinSettingKeys.Group, 11, 5, true, PluginId),
            SettingDefinition.Switch(DouyinSettingKeys.EnableIncremental,
                Strings.Get("LBL_DouyinEnableIncremental"), Strings.Get("DESC_DouyinEnableIncremental"),
                DouyinSettingKeys.Group, 11, 6, true, PluginId),
            SettingDefinition.Text(DouyinSettingKeys.FileTemplate,
                Strings.Get("LBL_DouyinFileTemplate"), Strings.Get("DESC_DouyinFileTemplate"),
                "{date}_{title}_{id}", DouyinSettingKeys.Group, 11, 7,
                new DouyinSettings().FileTemplate, PluginId),
            SettingDefinition.Text(DouyinSettingKeys.FolderTemplate,
                Strings.Get("LBL_DouyinFolderTemplate"), Strings.Get("DESC_DouyinFolderTemplate"),
                "{date}_{title}_{id}", DouyinSettingKeys.Group, 11, 8,
                new DouyinSettings().FolderStyle, PluginId),
            SettingDefinition.Text(DouyinSettingKeys.WebhookUrl,
                Strings.Get("LBL_DouyinWebhookUrl"), Strings.Get("DESC_DouyinWebhookUrl"),
                "https://example.com/hook", DouyinSettingKeys.Group, 11, 9, "", PluginId),
            SettingDefinition.Switch(DouyinSettingKeys.EnableTranscript,
                Strings.Get("LBL_DouyinEnableTranscript"), Strings.Get("DESC_DouyinEnableTranscript"),
                DouyinSettingKeys.Group, 11, 10, false, PluginId),
            SettingDefinition.Text(DouyinSettingKeys.TranscriptApiKey,
                Strings.Get("LBL_DouyinTranscriptApiKey"), Strings.Get("DESC_DouyinTranscriptApiKey"),
                "sk-...", DouyinSettingKeys.Group, 11, 11, "", PluginId),
            SettingDefinition.Text(DouyinSettingKeys.TranscriptModel,
                Strings.Get("LBL_DouyinTranscriptModel"), Strings.Get("DESC_DouyinTranscriptModel"),
                "gpt-4o-mini-transcribe", DouyinSettingKeys.Group, 11, 12,
                new DouyinSettings().TranscriptModel, PluginId),
            SettingDefinition.Switch(DouyinSettingKeys.EnableWebConsole,
                Strings.Get("LBL_DouyinEnableWebConsole"), Strings.Get("DESC_DouyinEnableWebConsole"),
                DouyinSettingKeys.Group, 11, 13, true, PluginId),
            SettingDefinition.Text(DouyinSettingKeys.WebConsoleHost,
                Strings.Get("LBL_DouyinWebConsoleHost"), null,
                "127.0.0.1", DouyinSettingKeys.Group, 11, 14,
                new DouyinSettings().WebConsoleHost, PluginId),
            SettingDefinition.Text(DouyinSettingKeys.WebConsolePort,
                Strings.Get("LBL_DouyinWebConsolePort"), null,
                "8765", DouyinSettingKeys.Group, 11, 15,
                new DouyinSettings().WebConsolePort.ToString(), PluginId),

            // ================ 抖音下载高级设置（原先只能手工改 settings.json） ================
            SettingDefinition.Text(DouyinSettingKeys.DatabasePath,
                Strings.Get("LBL_DouyinDatabasePath"), Strings.Get("DESC_DouyinDatabasePath"),
                "dy_downloader.db", DouyinSettingKeys.AdvancedGroup, 12, 0,
                new DouyinSettings().DatabasePath, PluginId),
            SettingDefinition.Text(DouyinSettingKeys.LiveMaxDurationSeconds,
                Strings.Get("LBL_DouyinLiveMaxDuration"), Strings.Get("DESC_DouyinLiveMaxDuration"),
                "0", DouyinSettingKeys.AdvancedGroup, 12, 1, "0", PluginId),
            SettingDefinition.Text(DouyinSettingKeys.LiveChunkSize,
                Strings.Get("LBL_DouyinLiveChunkSize"), Strings.Get("DESC_DouyinLiveChunkSize"),
                "65536", DouyinSettingKeys.AdvancedGroup, 12, 2, "65536", PluginId),
            SettingDefinition.Text(DouyinSettingKeys.LiveIdleTimeoutSeconds,
                Strings.Get("LBL_DouyinLiveIdleTimeout"), Strings.Get("DESC_DouyinLiveIdleTimeout"),
                "30", DouyinSettingKeys.AdvancedGroup, 12, 3, "30", PluginId),
            SettingDefinition.Switch(DouyinSettingKeys.BrowserFallbackEnabled,
                Strings.Get("LBL_DouyinBrowserFallback"), Strings.Get("DESC_DouyinBrowserFallback"),
                DouyinSettingKeys.AdvancedGroup, 12, 4, true, PluginId),
            SettingDefinition.Switch(DouyinSettingKeys.BrowserFallbackHeadless,
                Strings.Get("LBL_DouyinBrowserHeadless"), Strings.Get("DESC_DouyinBrowserHeadless"),
                DouyinSettingKeys.AdvancedGroup, 12, 5, false, PluginId),
            SettingDefinition.Text(DouyinSettingKeys.BrowserFallbackMaxScrolls,
                Strings.Get("LBL_DouyinBrowserMaxScrolls"), Strings.Get("DESC_DouyinBrowserMaxScrolls"),
                "240", DouyinSettingKeys.AdvancedGroup, 12, 6, "240", PluginId),
            SettingDefinition.Text(DouyinSettingKeys.BrowserFallbackIdleRounds,
                Strings.Get("LBL_DouyinBrowserIdleRounds"), Strings.Get("DESC_DouyinBrowserIdleRounds"),
                "8", DouyinSettingKeys.AdvancedGroup, 12, 7, "8", PluginId),
            SettingDefinition.Text(DouyinSettingKeys.BrowserFallbackWaitTimeoutSeconds,
                Strings.Get("LBL_DouyinBrowserWaitTimeout"), Strings.Get("DESC_DouyinBrowserWaitTimeout"),
                "600", DouyinSettingKeys.AdvancedGroup, 12, 8, "600", PluginId),
        ]);
    }
}

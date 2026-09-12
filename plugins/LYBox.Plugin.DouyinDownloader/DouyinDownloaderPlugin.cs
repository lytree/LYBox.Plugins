using LYBox.Plugin.DouyinDownloader.Auth;
using LYBox.Plugin.DouyinDownloader.Config;
using LYBox.Plugin.DouyinDownloader.Control;
using LYBox.Plugin.DouyinDownloader.Core;
using LYBox.Plugin.DouyinDownloader.Services;
using LYBox.Plugin.DouyinDownloader.Storage;
using LYBox.Plugin.DouyinDownloader.Utils;
using LYBox.Plugin.DouyinDownloader.WebServer;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace LYBox.Plugin.DouyinDownloader;

/// <summary>
/// 抖音下载器插件入口。
/// 注册：配置 / 签名 / API / 下载引擎 / 数据库 / Web 控制台 等所有单例服务。
/// </summary>
[GenerateMetadata]
[Menu("NAV_DouyinRoot", "DouyinDownloader_Root", Order = 50)]
public partial class DouyinDownloaderPlugin
{
    public Task InitializeAsync(IServiceCollection services)
    {
        // ---- 配置与设置（持久化） ----
        services.AddSingleton<PluginConfigStore>();
        services.AddSingleton<DownloaderSettingsStore>();

        // ---- 认证 (Cookie / msToken) ----
        services.AddSingleton<CookieManager>();
        services.AddSingleton<MsTokenManager>();

        // ---- 签名 (X-Bogus 1:1 移植 + 第三方签名服务封装) ----
        services.AddSingleton<SignatureClient>();

        // ---- 抖音 API 客户端 ----
        services.AddSingleton<DouyinApiClient>();

        // ---- 调度控制 ----
        services.AddSingleton<RateLimiter>();
        services.AddSingleton<RetryHandler>();
        services.AddSingleton<DownloadQueue>();

        // ---- 下载引擎 ----
        services.AddSingleton<MediaDownloader>();
        services.AddSingleton<LiveRecorder>();
        services.AddSingleton<LiveSessionRegistry>();
        services.AddSingleton<PlaywrightFallback>();
        services.AddSingleton<DownloaderFactory>();
        services.AddSingleton<DownloadOrchestrator>();

        // ---- 持久化 ----
        services.AddSingleton<DownloadDatabase>();

        // ---- Web 控制台 (可选) ----
        services.AddSingleton<WebConsoleServer>();

        // ---- 顶层协调器（页面 VM 通过 ServiceLocator 拉取） ----
        services.AddSingleton<DownloadCoordinator>();

        return Task.CompletedTask;
    }
}

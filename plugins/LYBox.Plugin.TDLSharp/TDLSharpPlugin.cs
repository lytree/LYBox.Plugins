using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.Shared.Models;
using LYBox.Plugin.Shared.Services;
using LYBox.Plugin.TDLSharp.Resources;
using LYBox.Plugin.TDLSharp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LYBox.Plugin.TDLSharp;

[GenerateMetadata]
public partial class TDLSharpPlugin
{
    /// <summary>宿主 Setting 中"运行状态"分组的 GroupName。</summary>
    private const string SettingsGroup = "TDL";

    public Task InitializeAsync(IServiceCollection services)
    {
        services.AddSingleton<TdlClientManager>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<TdlClientManager>();

            var (apiId, apiHash, proxyServer, proxyPort, enableProxy, tdlRootPath) = ResolveSettings(sp);

            return new TdlClientManager(logger, apiId, apiHash, proxyServer, proxyPort, enableProxy, tdlRootPath);
        });

        services.AddSingleton<TdlPluginStatusController>(sp =>
            new TdlPluginStatusController(sp.GetRequiredService<TdlClientManager>()));

        return Task.CompletedTask;
    }

    public Task RegisterAsync(IServiceProvider serviceProvider)
    {
        RegisterSettings(serviceProvider);
        // 触发 StatusController 构造（订阅 Action 事件、监听 AuthState 变更）。
        _ = serviceProvider.GetService<TdlPluginStatusController>();
        return Task.CompletedTask;
    }

    /// <summary>应用退出时显式释放 TdLib 客户端（原生资源）。Dispose 幂等，容器后续释放由守卫兜底。</summary>
    public Task ShutdownAsync()
    {
        if (ServiceLocator.TryGetService<TdlPluginStatusController>(out var controller))
        {
            controller.Dispose();
        }
        if (ServiceLocator.TryGetService<TdlClientManager>(out var manager))
        {
            manager.Dispose();
        }
        return Task.CompletedTask;
    }

    private void RegisterSettings(IServiceProvider serviceProvider)
    {
        var settingsService = serviceProvider.GetService<ISettingsService>();
        if (settingsService == null) return;

        // Group 0: 配置项（API、代理、目录）。Group 1: 运行期状态/操作（在配置项之后展示）。
        settingsService.RegisterSettings(
        [
            SettingDefinition.Path("TDL.TdlRootPath", Strings.Get("SETTING_TdlRootPath"), Strings.Get("SETTING_TdlRootPathDesc"),
                SettingsGroup, 0, 0, GetDefaultTdlRoot(), PluginId, isFolder: true),
            SettingDefinition.Text("TDL.ApiId", "API ID", "Telegram API ID", "", SettingsGroup, 0, 1,
                GetEnvDefault("tdl_api_id"), PluginId),
            SettingDefinition.Text("TDL.ApiHash", "API Hash", "Telegram API Hash", "", SettingsGroup, 0, 2,
                GetEnvDefault("tdl_api_hash"), PluginId),
            SettingDefinition.Text("TDL.ProxyServer", Strings.Get("SETTING_ProxyServer"), Strings.Get("SETTING_ProxyServerDesc"), "", SettingsGroup, 0, 3,
                "127.0.0.1", PluginId),
            SettingDefinition.Text("TDL.ProxyPort", Strings.Get("SETTING_ProxyPort"), Strings.Get("SETTING_ProxyPortDesc"), "", SettingsGroup, 0, 4,
                "7897", PluginId),
            SettingDefinition.Switch("TDL.EnableProxy", Strings.Get("SETTING_EnableProxy"), Strings.Get("SETTING_EnableProxyDesc"), SettingsGroup, 0, 5,
                true, PluginId),

            // 运行期状态：只读 + 三个 Action 卡片（初始化 / 扫码登录 / 退出）。
            SettingDefinition.ReadOnly(TdlPluginStatusController.StatusAuthState,
                Strings.Get("SETTING_TDL_Status"), Strings.Get("SETTING_TDL_StatusDesc"),
                SettingsGroup, 1, 0, Strings.Get("LOGIN_StatusIdle"), PluginId),
            SettingDefinition.Action(TdlPluginStatusController.ActionInitialize,
                Strings.Get("BTN_InitializeClient"), Strings.Get("BTN_InitializeClient_Tip"),
                SettingsGroup, 1, 1, null, PluginId),
            SettingDefinition.Action(TdlPluginStatusController.ActionQrLogin,
                Strings.Get("BTN_QrLogin"), Strings.Get("BTN_QrLogin_Tip"),
                SettingsGroup, 1, 2, null, PluginId),
            SettingDefinition.Action(TdlPluginStatusController.ActionLogout,
                Strings.Get("LOGIN_Logout"), Strings.Get("LOGIN_LogoutTip"),
                SettingsGroup, 1, 3, null, PluginId),
        ]);
    }

    private (string apiId, string apiHash, string proxyServer, int proxyPort, bool enableProxy, string tdlRootPath) ResolveSettings(IServiceProvider serviceProvider)
    {
        string apiId = GetSettingValue(serviceProvider, "TDL.ApiId", "tdl_api_id", "");
        string apiHash = GetSettingValue(serviceProvider, "TDL.ApiHash", "tdl_api_hash", "");
        string proxyServer = GetSettingValue(serviceProvider, "TDL.ProxyServer", "tdl_proxy_server", "127.0.0.1");
        string proxyPortStr = GetSettingValue(serviceProvider, "TDL.ProxyPort", "tdl_proxy_port", "7897");
        string enableProxyStr = GetSettingValue(serviceProvider, "TDL.EnableProxy", "tdl_enable_proxy", "true");
        string tdlRootPath = GetSettingValue(serviceProvider, "TDL.TdlRootPath", "tdl_root_path", GetDefaultTdlRoot());

        int proxyPort = int.TryParse(proxyPortStr, out var port) ? port : 7897;
        bool enableProxy = bool.TryParse(enableProxyStr, out var enabled) && enabled;

        return (apiId, apiHash, proxyServer, proxyPort, enableProxy, tdlRootPath);
    }

    private static string GetDefaultTdlRoot()
    {
        // 默认 TDLib 数据目录：Data/{PluginId}/tdl/（由 IPluginDataDirectoryProvider 解析）。
        return TdlPaths.DataSubdir("tdl");
    }

    private static string GetSettingValue(IServiceProvider serviceProvider, string settingKey, string envKey, string defaultValue)
    {
        var settingsService = serviceProvider.GetService<ISettingsService>();
        if (settingsService != null)
        {
            var value = settingsService.GetValue(settingKey);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        return GetEnvDefault(envKey) ?? defaultValue;
    }

    private static string? GetEnvDefault(string key)
    {
        return Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.User);
    }
}

using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.Shared.Models;
using Avalonia.Plugin.Shared.Services;
using Avalonia.Plugin.TDLSharp.Resources;
using Avalonia.Plugin.TDLSharp.Services;
using Microsoft.Extensions.Logging;

namespace Avalonia.Plugin.TDLSharp;

[GenerateMetadata]
public partial class TDLSharpPlugin : IPluginMetadata
{
    public string Name => "TDLSharp Plugin";
    public string Version => "1.0.0";
    public string Author => "TDLSharp";
    public string Description => "Telegram TDLib integration plugin providing batch forward, message export, media download and more.";
    public IEnumerable<string> Dependencies => [];
    public string PluginId => "A1B2C3D4-E5F6-7890-ABCD-TDLSHARP00001";

    public void Initialize()
    {
        if (ServiceLocator.TryGetService<ILocalizationService>(out var loc) && loc is not null)
            loc.RegisterResourceManager(Strings.ResourceManager);
        RegisterSettings();
        RegisterServices();
    }

    private void RegisterSettings()
    {
        if (!ServiceLocator.TryGetService<ISettingsService>(out var settingsService)) return;

        settingsService.RegisterSettings(
        [
            SettingDefinition.Text("TDL.ApiId", "API ID", "Telegram API ID", "","TDL", 0, 0,
                GetEnvDefault("tdl_api_id"), PluginId),
            SettingDefinition.Text("TDL.ApiHash", "API Hash", "Telegram API Hash", "","TDL", 0, 1,
                GetEnvDefault("tdl_api_hash"), PluginId),
            SettingDefinition.Text("TDL.ProxyServer", "代理服务器", "SOCKS5 代理服务器地址", "","TDL", 1, 0,
                "127.0.0.1", PluginId),
            SettingDefinition.Text("TDL.ProxyPort", "代理端口", "SOCKS5 代理端口","", "TDL", 1, 1,
                "7897", PluginId),
            SettingDefinition.Switch("TDL.EnableProxy", "启用代理", "是否启用 SOCKS5 代理", "TDL", 1, 2,
                true, PluginId),
        ]);
    }

    private void RegisterServices()
    {
        var (apiId, apiHash, proxyServer, proxyPort, enableProxy) = ResolveSettings();

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var clientManagerLogger = loggerFactory.CreateLogger<TdlClientManager>();
        var clientManager = new TdlClientManager(clientManagerLogger, apiId, apiHash, proxyServer, proxyPort, enableProxy);

        ServiceLocator.RegisterService<ILoggerFactory>(loggerFactory);
        ServiceLocator.RegisterService<TdlClientManager>(clientManager);
    }

    private (string apiId, string apiHash, string proxyServer, int proxyPort, bool enableProxy) ResolveSettings()
    {
        string apiId = GetSettingValue("TDL.ApiId", "tdl_api_id", "");
        string apiHash = GetSettingValue("TDL.ApiHash", "tdl_api_hash", "");
        string proxyServer = GetSettingValue("TDL.ProxyServer", "tdl_proxy_server", "127.0.0.1");
        string proxyPortStr = GetSettingValue("TDL.ProxyPort", "tdl_proxy_port", "7897");
        string enableProxyStr = GetSettingValue("TDL.EnableProxy", "tdl_enable_proxy", "true");

        int proxyPort = int.TryParse(proxyPortStr, out var port) ? port : 7897;
        bool enableProxy = bool.TryParse(enableProxyStr, out var enabled) && enabled;

        return (apiId, apiHash, proxyServer, proxyPort, enableProxy);
    }

    private static string GetSettingValue(string settingKey, string envKey, string defaultValue)
    {
        if (ServiceLocator.TryGetService<ISettingsService>(out var settingsService))
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

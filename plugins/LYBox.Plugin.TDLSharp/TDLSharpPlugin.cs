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
        // 启动期环境变量快照：只看与本插件相关的，避免一次性全量刷屏。
        DumpStartupEnvironment();

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
        // 注入宿主环境：TDLSharp 后续所有路径与日志都通过 IPluginHostEnvironment 解析，不再硬编码 fallback。
        // 通过 IPluginHostEnvironmentFactory 按 PluginId 拿到独立的宿主环境实例：
        // - 独立的 ILoggerFactory → 日志落到 logs/plugins/{PluginId}/，与宿主主日志隔离
        // - 共用路径解析（HostDataRoot / AppBaseDirectory / LogsDirectory）
        var factory = serviceProvider.GetService<IPluginHostEnvironmentFactory>();
        var hostEnv = factory?.Create(TdlPaths.PluginId);
        var dataDir = serviceProvider.GetService<IPluginDataDirectoryProvider>();
        TdlPaths.Initialize(hostEnv, dataDir);

        // 启动期路径 dump：现在 TdlPaths 已注入宿主环境，路径值是"宿主视角"的真实值。
        DumpHostPathsAfterInit();

        // 触发 StatusController 构造（订阅 Action 事件、监听 AuthState 变更）。
        _ = serviceProvider.GetService<TdlPluginStatusController>();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 启动期路径 dump：TdlPaths.Initialize 之后调用，所有路径值均来自宿主 <see cref="IPluginHostEnvironment"/>。
    /// 统一日志管理：日志直接交给宿主的 <see cref="ILoggerFactory"/>（控制台 + 滚动日志文件），
    /// 不再单独写 plugin-startup.log 文件。
    /// </summary>
    private static void DumpHostPathsAfterInit()
    {
        // 统一日志管理：通过 TdlPaths.LoggerFactory（= IPluginHostEnvironment.LoggerFactory）
        // 创建的 ILogger 自动落到宿主的控制台与滚动日志中。
        ILogger? logger = TdlPaths.IsInitialized
            ? TdlPaths.LoggerFactory.CreateLogger("LYBox.Plugin.TDLSharp")
            : null;

        if (!TdlPaths.IsInitialized)
        {
            const string msg = "[TDLSharp] 启动期路径 dump 跳过：TdlPaths 未初始化。";
            logger?.LogWarning(msg);
            return;
        }

        string Safe(string label, string value)
        {
            try { return $"  {label,-18} = {value}"; }
            catch (Exception ex) { return $"  {label,-18} = <error: {ex.Message}>"; }
        }

        var lines = new[]
        {
            Safe("DataRoot",          TdlPaths.DataRoot),
            Safe("HistoryDir",        TdlPaths.HistoryDir),
            Safe("ForwardDbDir",      TdlPaths.ForwardDbDir),
            Safe("LogsDirectory",     TdlPaths.LogsDirectory),
            Safe("PluginLogsDir",     TdlPaths.PluginLogsDirectory),
            Safe("AppBaseDirectory",  TdlPaths.AppBaseDirectory),
            Safe("AppVersion",        TdlPaths.AppVersion),
            Safe("IsPortableMode",    TdlPaths.IsPortableMode.ToString()),
            Safe("PluginId",          TdlPaths.PluginId),
        };

        // 统一日志：仅通过 ILogger 写入。Console 不再额外输出。
        logger.LogInformation("==== TDLSharp 启动期路径 dump (via IPluginHostEnvironment) ====");
        foreach (var line in lines) logger.LogInformation(line);
        logger.LogInformation("==== dump 结束 ====");
    }

    /// <summary>应用退出时显式释放 TdLib 客户端（原生资源）。Dispose 幂等，容器后续释放由守卫兜底。</summary>
    public Task ShutdownAsync()
    {
        if (ServiceLocator.TryGetService<TdlPluginStatusController>(out var controller) && controller is not null)
        {
            controller.Dispose();
        }
        if (ServiceLocator.TryGetService<TdlClientManager>(out var manager) && manager is not null)
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
        // TDLib 数据目录优先使用用户在设置页显式配置的值；未配置时回落默认 %USERPROFILE%\.tdl。
        // 不再使用 %APPDATA% 或 tdl_root_path 用户环境变量。
        string tdlRootPath = GetSettingValue(serviceProvider, "TDL.TdlRootPath", null, GetDefaultTdlRoot())
            ?? GetDefaultTdlRoot();

        int proxyPort = int.TryParse(proxyPortStr, out var port) ? port : 7897;
        bool enableProxy = bool.TryParse(enableProxyStr, out var enabled) && enabled;

        return (apiId, apiHash, proxyServer, proxyPort, enableProxy, tdlRootPath);
    }

    private static string GetDefaultTdlRoot()
    {
        // 默认 TDLib 数据目录：{UserHome}/.tdl（按当前用户隔离，与系统 / 程序目录解耦）。
        // 注意：这是本插件的约定，TDLib 本身没有默认目录。
        var profile = ServiceLocator.GetService<LYBox.Plugin.Shared.Services.IRuntimeProfile>();
        var home = profile?.UserHomeDirectory
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".tdl");
    }

    private static string GetSettingValue(IServiceProvider serviceProvider, string settingKey, string? envKey, string defaultValue)
    {
        var settingsService = serviceProvider.GetService<ISettingsService>();
        if (settingsService != null)
        {
            var value = settingsService.GetValue(settingKey);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        // envKey 为 null 时跳过环境变量兜底，避免污染默认目录（典型场景：TDL.TdlRootPath）。
        if (envKey is null) return defaultValue;
        return GetEnvDefault(envKey) ?? defaultValue;
    }

    private static string? GetEnvDefault(string key)
    {
        return Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.User);
    }

    /// <summary>
    /// 启动期环境变量快照：只关心本插件相关的 Key（其余全量环境变量会刷屏）。
    /// 同时输出到 Console（控制台 / 重定向 stdout 都能看到）与宿主滚动日志（如果已就绪）。
    /// </summary>
    private void DumpStartupEnvironment()
    {
        // 与本插件读取 / 影响的 Key 列表，集中维护便于对齐。
        // 注意：插件本身读取的 envKey 见 GetSettingValue 调用集合（tdl_api_id / tdl_api_hash 等）。
        var keys = new[]
        {
            // 宿主相关：数据根目录是否被用户/CI 覆盖。
            "LYBOX_DATA_ROOT",
            // TDLSharp 自身关心的环境变量（默认 / 兜底取值）。
            "tdl_api_id",
            "tdl_api_hash",
            "tdl_proxy_server",
            "tdl_proxy_port",
            "tdl_enable_proxy",
            "tdl_root_path",
            // 调试 / 日志相关。
            "WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS",
            "LYBOX_WEB_PORT",
            "DOTNET_ENVIRONMENT",
        };

        // 构造快照行：每行 "KEY = VALUE   [effective=Process/User/Machine/-]"。
        // 这里分别查三个范围，便于一眼看到"实际生效"的值（按 Process > User > Machine 优先级）。
        var lines = new List<string>(keys.Length + 8);
        foreach (var key in keys)
        {
            var proc = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Process);
            var user = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.User);
            var mach = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Machine);
            string effective;
            string scope;
            if (!string.IsNullOrEmpty(proc)) { effective = proc; scope = "Process"; }
            else if (!string.IsNullOrEmpty(user)) { effective = user; scope = "User"; }
            else if (!string.IsNullOrEmpty(mach)) { effective = mach; scope = "Machine"; }
            else { effective = "<not set>"; scope = "-"; }

            // 隐藏敏感字段：API Hash 不应打到日志里。
            var displayValue = key.Contains("HASH", StringComparison.OrdinalIgnoreCase)
                ? (string.IsNullOrEmpty(effective) ? "<not set>" : "<redacted, " + effective.Length + " chars>")
                : effective;

            lines.Add($"  {key,-34} = {displayValue}   [effective={scope}]");
        }

        // 统一日志：通过 PluginLoggers 拿到 ILogger，未初始化时返回 NullLogger 静默丢弃。
        var logger = PluginLoggers.For("LYBox.Plugin.TDLSharp");
        logger.LogInformation("==== TDLSharp 启动期环境变量快照 ====");
        foreach (var line in lines) logger.LogInformation(line);
        logger.LogInformation("==== TDLSharp 启动期环境变量快照 结束 ====");
    }
}

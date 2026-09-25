using LYBox.Plugin.Downloader.Config;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Services;
using Microsoft.Extensions.Logging;

namespace LYBox.Plugin.Downloader.Utils;

/// <summary>
/// Downloader 子模块的日志门面(向后兼容)。
/// <para>
/// 旧实现:静态委托 <see cref="Action{T}"/> Sink,需要宿主显式挂载,否则日志被吞掉。
/// 新实现:内部委托到宿主 <see cref="IPluginHostEnvironment.LoggerFactory"/>(每个插件一个独立通道),
/// 由宿主 ZLogger 统一落盘 + 控制台输出。无需再 <c>Logger.Sink = ...</c>。
/// </para>
/// <para>
/// 保留 <c>Info / Warn / Error</c> 公共方法签名以便最小改动;Sink 委托已废弃(保留属性仅为外部代码不会编译失败)。
/// </para>
/// </summary>
public static class Logger
{
    /// <summary>类别名(ZLogger 控制台 / 滚动日志的 Category 字段)。</summary>
    private const string CategoryName = "LYBox.Plugin.Downloader";

    /// <summary>
    /// 已废弃:不再需要外部挂载 Sink。保留仅为不破坏调用方编译;
    /// 设置值会被忽略——日志统一走宿主 <see cref="IPluginHostEnvironment.LoggerFactory"/>。
    /// </summary>
    [Obsolete("Downloader 日志已统一经宿主 IPluginHostEnvironment.LoggerFactory 落盘,无需再挂 Sink。")]
    public static Action<string>? Sink
    {
        get => null;
        set { /* ignore — 统一日志已接管 */ }
    }

    /// <summary>解析当前插件的 <see cref="ILogger"/>(走 <see cref="IPluginHostEnvironment"/>)。</summary>
    private static ILogger? Resolve()
    {
        if (ServiceLocator.TryGetService<IPluginHostEnvironmentFactory>(out var factory))
        {
            // 每个插件一个 ILoggerFactory;此处用本插件的 PluginId。
            var hostEnv = factory?.Create(PluginConfigStore.PluginId);
            return hostEnv?.LoggerFactory.CreateLogger(CategoryName);
        }
        return null;
    }

    public static void Info(string s)
    {
        Resolve()?.LogInformation("{Message}", s);
    }

    public static void Warn(string s)
    {
        Resolve()?.LogWarning("{Message}", s);
    }

    public static void Error(string s)
    {
        Resolve()?.LogError("{Message}", s);
    }
}
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Paths;
using LYBox.Plugin.Shared.Services;

namespace LYBox.Plugin.Downloader.Config;

/// <summary>插件数据目录解析(统一入口)。</summary>
public sealed class PluginConfigStore
{
    /// <summary>本插件 ID,必须与 csproj 的 <c>PluginId</c> 保持一致。</summary>
    public const string PluginId = "B2C3D4E5-F6A7-8901-BCDE-DOWNLOADER001";

    /// <summary>
    /// 抖音子模块在插件数据根目录下的子文件夹名。
    /// 单独占一层是必要的:根目录的 <c>settings.json</c> 属于 <c>Services.BinaryPathsStore</c>
    /// (外部二进制路径,结构不同),两者混用会互相覆盖。
    /// 该常量与 SDK <see cref="PluginSubDirectories.Douyin"/> 保持一致,便于跨插件对齐。
    /// </summary>
    public const string DouyinSubFolder = PluginSubDirectories.Douyin;

    /// <summary>
    /// 历史独立插件 <c>LYBox.Plugin.DouyinDownloader</c> 的数据目录 ID。
    /// 仅用于一次性迁移(见 <see cref="PluginDataMigration"/>),迁移后不再写入。
    /// </summary>
    public const string LegacyDouyinSubId = "DouyinDownloader";

    private readonly IPluginDataDirectoryProvider? _provider;

    public PluginConfigStore()
    {
        ServiceLocator.TryGetService<IPluginDataDirectoryProvider>(out _provider);
    }

    /// <summary>插件数据根目录(<c>Data/{PluginId}/</c>)。</summary>
    public string RootDir => ResolveRootDir(_provider);

    /// <summary>抖音子模块数据目录(<c>Data/{PluginId}/douyin/</c>)。</summary>
    public string DouyinDir => ResolveDouyinDir(_provider);

    /// <summary>抖音子模块数据目录下的文件路径(settings.json / cookies.json / *.db 等)。</summary>
    public string ResolvePath(string sub) => Path.Combine(DouyinDir, sub);

    /// <summary>
    /// 从 <see cref="ServiceLocator"/> 取数据目录提供器;ServiceLocator 尚未初始化时返回 <c>null</c>。
    /// </summary>
    public static IPluginDataDirectoryProvider? CurrentProvider
        => ServiceLocator.TryGetService<IPluginDataDirectoryProvider>(out IPluginDataDirectoryProvider? p) ? p : null;

    /// <summary>
    /// 解析插件数据根目录。
    /// 要求 <see cref="IPluginDataDirectoryProvider"/> 在 DI 中可用(由宿主在 <c>App.Initialize()</c> 早期注册);
    /// 提供器缺失时抛 <see cref="InvalidOperationException"/>(不再静默回退 <c>AppContext.BaseDirectory</c>,
    /// 避免自包含发布 / 只读权限场景写入失败)。
    /// </summary>
    public static string ResolveRootDir(IPluginDataDirectoryProvider? provider)
        => provider is not null
            ? provider.GetPluginDataDirectory(PluginId)
            : throw new InvalidOperationException(
                $"插件数据目录提供器未注册。请确认宿主在 DI 早期已注册 {nameof(IPluginDataDirectoryProvider)},"
                + $"并通过 ServiceLocator 解析后再调用 {nameof(PluginConfigStore)}.{nameof(ResolveRootDir)}。");

    /// <summary>解析抖音子模块数据目录。</summary>
    public static string ResolveDouyinDir(IPluginDataDirectoryProvider? provider)
        => provider is not null
            ? provider.GetPluginDouyinDirectory(PluginId)
            : throw new InvalidOperationException(
                $"插件数据目录提供器未注册。请确认宿主在 DI 早期已注册 {nameof(IPluginDataDirectoryProvider)},"
                + $"并通过 ServiceLocator 解析后再调用 {nameof(PluginConfigStore)}.{nameof(ResolveDouyinDir)}。");

    /// <summary>
    /// 解析历史数据目录(<c>Data/DouyinDownloader/</c>)。
    /// 直接用 <see cref="IPluginDataDirectoryProvider.HostDataRoot"/> 拼接而非
    /// <c>GetPluginDataDirectory</c>,避免查询旧目录时顺手把空目录建出来。
    /// </summary>
    public static string ResolveLegacyDouyinDir(IPluginDataDirectoryProvider provider)
        => Path.Combine(provider.HostDataRoot, LegacyDouyinSubId);
}

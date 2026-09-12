using LYBox.Plugin.Shared.Services;

namespace LYBox.Plugin.DouyinDownloader.Config;

/// <summary>插件数据目录解析（统一入口）。</summary>
public sealed class PluginConfigStore
{
    public const string PluginId = "DouyinDownloader";
    private readonly IPluginDataDirectoryProvider? _provider;

    public PluginConfigStore()
    {
        LYBox.Plugin.Shared.ServiceLocator.TryGetService<IPluginDataDirectoryProvider>(out _provider);
    }

    public string DataDir => _provider?.GetSubDirectory(PluginId, "") ?? Path.Combine(AppContext.BaseDirectory, "PluginData", PluginId);

    public string ResolvePath(string sub) => Path.Combine(DataDir, sub);
}

using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Services;

namespace LYBox.Plugin.Downloader.Douyin.Config;

/// <summary>插件数据目录解析（统一入口）。</summary>
public sealed class PluginConfigStore
{
    /// <summary>为避免与 Douyin 子模块外部 const 冲突,改用实例属性。</summary>
    public const string DouyinSubId = "DouyinDownloader";

    private readonly IPluginDataDirectoryProvider? _provider;

    public PluginConfigStore()
    {
        ServiceLocator.TryGetService<IPluginDataDirectoryProvider>(out _provider);
    }

    public string DataDir => _provider?.GetPluginDataDirectory(DouyinSubId) ?? Path.Combine(AppContext.BaseDirectory, "PluginData", DouyinSubId);

    public string ResolvePath(string sub) => Path.Combine(DataDir, sub);
}

using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Services;

namespace LYBox.Plugin.Downloader.Douyin.Config;

/// <summary>插件数据目录解析（统一入口）。</summary>
public sealed class PluginConfigStore
{
    /// <summary>抖音子模块 ID（必须与历史 LYBox.Plugin.DouyinDownloader 的 csproj PluginId 保持一致,用于复用旧数据目录）。</summary>
    public const string DouyinSubId = "DouyinDownloader";

    private readonly IPluginDataDirectoryProvider? _provider;

    public PluginConfigStore()
    {
        ServiceLocator.TryGetService<IPluginDataDirectoryProvider>(out _provider);
    }

    public string DataDir => _provider?.GetPluginDataDirectory(DouyinSubId) ?? Path.Combine(AppContext.BaseDirectory, "PluginData", DouyinSubId);

    public string ResolvePath(string sub) => Path.Combine(DataDir, sub);
}

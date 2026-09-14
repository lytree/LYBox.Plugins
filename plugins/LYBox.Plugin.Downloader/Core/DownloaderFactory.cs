using LYBox.Plugin.Downloader.Core.Strategies;
using LYBox.Plugin.Downloader.Models;

namespace LYBox.Plugin.Downloader.Core;

/// <summary>URL → 策略 映射工厂（对齐 core/downloader_factory.py + user_mode_registry.py）。</summary>
public sealed class DownloaderFactory
{
    private readonly Dictionary<(UrlKind, DownloadMode), IDownloadStrategy> _map = new();
    private readonly VideoAndGalleryStrategy _video = new();
    private readonly MixStrategy _mix = new();
    private BrowserFallbackStrategy? _browser;
    private LiveStrategy? _live;
    private LiveReplayStrategy? _liveReplay;

    public DownloaderFactory(LiveSessionRegistry liveRegistry, PlaywrightFallback? browser = null)
    {
        Register(_video);
        Register(new UserPostStrategy());
        Register(new UserLikeStrategy());
        Register(new UserCollectStrategy());
        Register(new UserCollectMixStrategy());
        Register(_mix);
        if (browser != null)
            _browser = new BrowserFallbackStrategy(browser);
        _live = new LiveStrategy(liveRegistry);
        _map[(UrlKind.Live, DownloadMode.Post)] = _live;
        _liveReplay = new LiveReplayStrategy();
        _map[(UrlKind.LiveReplay, DownloadMode.Post)] = _liveReplay;
    }

    /// <summary>获取兜底浏览器策略（仅在 API 失败时由 DouyinDownloadOrchestrator 主动升级调用,不会自动劫持 User/Post）。</summary>
    public BrowserFallbackStrategy? BrowserFallback => _browser;

    private void Register(IDownloadStrategy s)
    {
        foreach (var k in s.SupportedKinds)
            _map[(k, s.Mode)] = s;
    }

    public IDownloadStrategy? Resolve(UrlKind kind, DownloadMode mode) =>
        _map.TryGetValue((kind, mode), out var s) ? s : null;

    public IDownloadStrategy ResolveOrFallback(UrlKind kind, DownloadMode mode) =>
        Resolve(kind, mode) ?? _video;
}

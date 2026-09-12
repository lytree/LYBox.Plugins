using LYBox.Plugin.DouyinDownloader.Config;
using LYBox.Plugin.DouyinDownloader.Models;

namespace LYBox.Plugin.DouyinDownloader.Core.Strategies;

/// <summary>下载策略接口（与原项目 core/user_modes/base_strategy.py 对齐）。</summary>
public interface IDownloadStrategy
{
    /// <summary>对应的 URL 类型/模式。</summary>
    UrlKind[] SupportedKinds { get; }
    DownloadMode Mode { get; }

    /// <summary>实际下载。返回 (成功数, 失败数, 跳过数)。</summary>
    Task<(int Success, int Failed, int Skipped)> ExecuteAsync(JobContext ctx, CancellationToken ct);
}

public sealed record JobContext(
    string RootDir,
    DownloadJobRow Row,
    ParsedUrl Parsed,
    DownloaderSettingsStore Settings,
    DouyinApiClient Api,
    MediaDownloader Media,
    Storage.DownloadDatabase Db,
    Control.RateLimiter RateLimiter,
    Action<string>? Log = null);

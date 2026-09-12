using LYBox.Plugin.DouyinDownloader.Models;
using LYBox.Plugin.DouyinDownloader.Utils;

namespace LYBox.Plugin.DouyinDownloader.Core.Strategies;

/// <summary>
/// 浏览器兜底策略：用户主页批量下载时,如果 API 受限,用 Playwright 启动 Chromium 滚动加载,
/// 拿到全部 aweme_id 后再走 VideoAndGalleryStrategy 逐个拉取详情下载。
/// </summary>
public sealed class BrowserFallbackStrategy : IDownloadStrategy
{
    private readonly PlaywrightFallback _fallback;

    public BrowserFallbackStrategy(PlaywrightFallback fallback) { _fallback = fallback; }

    public UrlKind[] SupportedKinds => new[] { UrlKind.User };
    public DownloadMode Mode => DownloadMode.Post;

    public async Task<(int Success, int Failed, int Skipped)> ExecuteAsync(JobContext ctx, CancellationToken ct)
    {
        var secUid = ctx.Parsed.SecUid!;
        ctx.Log?.Invoke("API 翻页受限,启动浏览器兜底");
        var browserResult = await _fallback.CollectUserPostIdsAsync(secUid, ctx.Row.Number, ct);
        if (browserResult.AwemeIds.Count == 0)
        {
            ctx.Log?.Invoke($"浏览器兜底未取到作品 (stop_reason={browserResult.StopReason})");
            return (0, 1, 0);
        }
        ctx.Log?.Invoke($"浏览器兜底: 共 {browserResult.AwemeIds.Count} 个 aweme_id (来自 {browserResult.FromPostApi} API, 来自 DOM {browserResult.Merged - browserResult.FromPostApi})");

        var root = Path.Combine(ctx.RootDir, UrlParser.SanitizeFilename(secUid));
        Directory.CreateDirectory(root);
        int success = 0, failed = 0, skipped = 0;
        var limit = ctx.Row.Number;
        foreach (var awemeId in browserResult.AwemeIds)
        {
            if (ct.IsCancellationRequested) break;
            if (limit > 0 && success + failed + skipped >= limit) break;
            if (ctx.Settings.Current.Database && ctx.Db.Exists(awemeId)) { skipped++; continue; }
            await ctx.RateLimiter.AcquireAsync(ct);
            var detail = await ctx.Api.GetVideoDetailAsync(awemeId, ct);
            if (detail == null) { failed++; continue; }
            var ok = await VideoAndGalleryStrategy.DownloadOneAsync(ctx with { RootDir = root }, detail, ct);
            if (ok) success++; else failed++;
        }
        return (success, failed, skipped);
    }
}

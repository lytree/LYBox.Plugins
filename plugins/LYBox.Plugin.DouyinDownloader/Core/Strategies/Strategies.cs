using LYBox.Plugin.DouyinDownloader.Models;
using LYBox.Plugin.DouyinDownloader.Utils;

namespace LYBox.Plugin.DouyinDownloader.Core.Strategies;

internal static class StrategyHelpers
{
    /// <summary>解析短链为真实 URL（与 core/url_parser.py 的 resolve_short_url 一致）。</summary>
    public static async Task<string?> ResolveAsync(DouyinApiClient api, string url, CancellationToken ct)
    {
        if (!UrlParser.IsShortUrl(url)) return url;
        return await api.ResolveShortUrlAsync(url, ct);
    }

    public static async Task<(int Success, int Failed, int Skipped)> DownloadAllItemsAsync(
        JobContext ctx,
        Func<PagedResult<AwemeListItem>, Task<bool>> shouldStop,
        Func<AwemeListItem, Task<(bool ok, bool skip)>> onItem,
        CancellationToken ct)
    {
        long cursor = 0;
        int success = 0, failed = 0, skipped = 0;
        while (!ct.IsCancellationRequested)
        {
            await ctx.RateLimiter.AcquireAsync(ct);
            var res = await FetchPage(ctx, cursor, ct);
            if (res.Items.Count == 0) break;
            foreach (var item in res.Items)
            {
                if (await shouldStop(res)) return (success, failed, skipped);
                var (ok, skip) = await onItem(item);
                if (skip) skipped++;
                else if (ok) success++;
                else failed++;
            }
            if (!res.HasMore) break;
            cursor = res.MaxCursor;
        }
        return (success, failed, skipped);
    }

    private static Task<PagedResult<AwemeListItem>> FetchPage(JobContext ctx, long cursor, CancellationToken ct) =>
        ctx.Api.GetUserPostAsync(ctx.Parsed.SecUid!, cursor, 18, ct);

    public static string AuthorDir(string root, string author) =>
        Path.Combine(root, UrlParser.SanitizeFilename(string.IsNullOrWhiteSpace(author) ? "unknown" : author));

    public static string ItemDir(string authorDir, string itemName) => Path.Combine(authorDir, itemName);
}

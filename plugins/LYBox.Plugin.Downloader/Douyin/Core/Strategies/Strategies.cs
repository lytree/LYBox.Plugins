using LYBox.Plugin.Downloader.Douyin.Models;

namespace LYBox.Plugin.Downloader.Douyin.Core.Strategies;

internal static class StrategyHelpers
{
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
}

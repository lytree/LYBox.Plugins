using LYBox.Plugin.DouyinDownloader.Models;
using LYBox.Plugin.DouyinDownloader.Storage;
using LYBox.Plugin.DouyinDownloader.Utils;

namespace LYBox.Plugin.DouyinDownloader.Core.Strategies;

/// <summary>用户作品批量下载（post / like / mix / music / collect / collectmix）。</summary>
public abstract class UserModeStrategyBase : IDownloadStrategy
{
    public abstract UrlKind[] SupportedKinds { get; }
    public abstract DownloadMode Mode { get; }

    public async Task<(int Success, int Failed, int Skipped)> ExecuteAsync(JobContext ctx, CancellationToken ct)
    {
        var root = Path.Combine(ctx.RootDir, UrlParser.SanitizeFilename(ctx.Parsed.SecUid ?? "self"));
        Directory.CreateDirectory(root);

        var fetched = 0;
        var limit = ctx.Row.Number; // 0 = 不限
        var stop = false;
        var (s, f, sk) = await StrategyHelpers.DownloadAllItemsAsync(ctx,
            shouldStop: res =>
            {
                if (limit > 0 && fetched >= limit) { stop = true; return Task.FromResult(true); }
                return Task.FromResult(false);
            },
            onItem: async item =>
            {
                if (limit > 0 && fetched >= limit) { stop = true; return (false, true); }
                fetched++;
                if (ctx.Settings.Current.Database && ctx.Db.Exists(item.AwemeId))
                {
                    ctx.Log?.Invoke($"跳过: {item.AwemeId}");
                    return (false, true);
                }
                await ctx.RateLimiter.AcquireAsync(ct);
                var detail = await ctx.Api.GetVideoDetailAsync(item.AwemeId, ct);
                if (detail == null)
                {
                    ctx.Log?.Invoke($"详情失败: {item.AwemeId}");
                    return (false, false);
                }
                // 复用 VideoAndGalleryStrategy 下载器
                var scoped = ctx with { RootDir = root };
                var ok = await VideoAndGalleryStrategy.DownloadOneAsync(scoped, detail, ct);
                return ok ? (true, false) : (false, false);
            },
            ct);
        return (s, f, sk);
    }
}

public sealed class UserPostStrategy : UserModeStrategyBase
{
    public override UrlKind[] SupportedKinds => new[] { UrlKind.User };
    public override DownloadMode Mode => DownloadMode.Post;
}

public sealed class UserLikeStrategy : UserModeStrategyBase
{
    public override UrlKind[] SupportedKinds => new[] { UrlKind.User };
    public override DownloadMode Mode => DownloadMode.Like;
}

/// <summary>当前登录账号收藏作品（self）。</summary>
public sealed class UserCollectStrategy : IDownloadStrategy
{
    public UrlKind[] SupportedKinds => new[] { UrlKind.User };
    public DownloadMode Mode => DownloadMode.Collect;

    public async Task<(int Success, int Failed, int Skipped)> ExecuteAsync(JobContext ctx, CancellationToken ct)
    {
        if (ctx.Parsed.SecUid != "self") return (0, 0, 0);
        var root = Path.Combine(ctx.RootDir, "collect");
        Directory.CreateDirectory(root);
        long cursor = 0;
        int success = 0, failed = 0, skipped = 0;
        int limit = ctx.Row.Number;
        while (!ct.IsCancellationRequested)
        {
            await ctx.RateLimiter.AcquireAsync(ct);
            var page = await ctx.Api.GetUserCollectionAsync("self", cursor, 20, ct);
            if (page.Items.Count == 0) break;
            foreach (var item in page.Items)
            {
                if (limit > 0 && success + failed + skipped >= limit) return (success, failed, skipped);
                if (ctx.Settings.Current.Database && ctx.Db.Exists(item.AwemeId))
                {
                    skipped++; continue;
                }
                await ctx.RateLimiter.AcquireAsync(ct);
                var detail = await ctx.Api.GetVideoDetailAsync(item.AwemeId, ct);
                if (detail == null) { failed++; continue; }
                var ok = await VideoAndGalleryStrategy.DownloadOneAsync(ctx with { RootDir = root }, detail, ct);
                if (ok) success++; else failed++;
            }
            if (!page.HasMore) break;
            cursor = page.MaxCursor;
        }
        return (success, failed, skipped);
    }
}

/// <summary>收藏合集（self collectmix）。</summary>
public sealed class UserCollectMixStrategy : IDownloadStrategy
{
    public UrlKind[] SupportedKinds => new[] { UrlKind.User };
    public DownloadMode Mode => DownloadMode.CollectMix;

    public async Task<(int Success, int Failed, int Skipped)> ExecuteAsync(JobContext ctx, CancellationToken ct)
    {
        if (ctx.Parsed.SecUid != "self") return (0, 0, 0);
        var root = Path.Combine(ctx.RootDir, "collectmix");
        Directory.CreateDirectory(root);
        long cursor = 0;
        int success = 0, failed = 0;
        int limit = ctx.Row.Number;
        while (!ct.IsCancellationRequested)
        {
            await ctx.RateLimiter.AcquireAsync(ct);
            var page = await ctx.Api.GetUserCollectMixAsync("self", cursor, 12, ct);
            if (page.Items.Count == 0) break;
            foreach (var mix in page.Items)
            {
                if (limit > 0 && success + failed >= limit) return (success, failed, 0);
                // 取合集首条作品
                await ctx.RateLimiter.AcquireAsync(ct);
                var mp = await ctx.Api.GetMixAwemeAsync(mix.MixId, 0, 1, ct);
                var first = mp.Items.FirstOrDefault();
                if (first == null) { failed++; continue; }
                if (ctx.Settings.Current.Database && ctx.Db.Exists(first.AwemeId))
                {
                    success++; // 不计入下载数,但避免失败
                    continue;
                }
                await ctx.RateLimiter.AcquireAsync(ct);
                var detail = await ctx.Api.GetVideoDetailAsync(first.AwemeId, ct);
                if (detail == null) { failed++; continue; }
                var ok = await VideoAndGalleryStrategy.DownloadOneAsync(ctx with { RootDir = root }, detail, ct);
                if (ok) success++; else failed++;
            }
            if (!page.HasMore) break;
            cursor = page.MaxCursor;
        }
        return (success, failed, 0);
    }
}

/// <summary>合集（mix）下载。</summary>
public sealed class MixStrategy : IDownloadStrategy
{
    public UrlKind[] SupportedKinds => new[] { UrlKind.Collection };
    public DownloadMode Mode => DownloadMode.Mix;

    public async Task<(int Success, int Failed, int Skipped)> ExecuteAsync(JobContext ctx, CancellationToken ct)
    {
        var mixId = ctx.Parsed.MixId!;
        var root = Path.Combine(ctx.RootDir, "mix_" + mixId);
        Directory.CreateDirectory(root);
        long cursor = 0;
        int success = 0, failed = 0, skipped = 0;
        int limit = ctx.Row.Number;
        while (!ct.IsCancellationRequested)
        {
            await ctx.RateLimiter.AcquireAsync(ct);
            var page = await ctx.Api.GetMixAwemeAsync(mixId, cursor, 20, ct);
            if (page.Items.Count == 0) break;
            foreach (var item in page.Items)
            {
                if (limit > 0 && success + failed + skipped >= limit) return (success, failed, skipped);
                if (ctx.Settings.Current.Database && ctx.Db.Exists(item.AwemeId)) { skipped++; continue; }
                await ctx.RateLimiter.AcquireAsync(ct);
                var detail = await ctx.Api.GetVideoDetailAsync(item.AwemeId, ct);
                if (detail == null) { failed++; continue; }
                var ok = await VideoAndGalleryStrategy.DownloadOneAsync(ctx with { RootDir = root }, detail, ct);
                if (ok) success++; else failed++;
            }
            if (!page.HasMore) break;
            cursor = page.MaxCursor;
        }
        return (success, failed, skipped);
    }
}

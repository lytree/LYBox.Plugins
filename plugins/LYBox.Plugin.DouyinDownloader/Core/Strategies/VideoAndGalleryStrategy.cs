using System.Text.Json;
using LYBox.Plugin.DouyinDownloader.Models;
using LYBox.Plugin.DouyinDownloader.Storage;
using LYBox.Plugin.DouyinDownloader.Utils;

namespace LYBox.Plugin.DouyinDownloader.Core.Strategies;

/// <summary>单视频/图文下载（对应 video_downloader.py + gallery）。</summary>
public sealed class VideoAndGalleryStrategy : IDownloadStrategy
{
    public UrlKind[] SupportedKinds => new[] { UrlKind.Video, UrlKind.Note };
    public DownloadMode Mode => DownloadMode.Post;

    public async Task<(int Success, int Failed, int Skipped)> ExecuteAsync(JobContext ctx, CancellationToken ct)
    {
        var awemeId = ctx.Parsed.AwemeId!;
        if (ctx.Settings.Current.Database && ctx.Db.Exists(awemeId))
        {
            ctx.Log?.Invoke($"跳过已下载: {awemeId}");
            return (0, 0, 1);
        }
        await ctx.RateLimiter.AcquireAsync(ct);
        var detail = await ctx.Api.GetVideoDetailAsync(awemeId, ct);
        if (detail == null)
        {
            ctx.Log?.Invoke($"获取详情失败: {awemeId}");
            return (0, 1, 0);
        }
        var ok = await DownloadOneAsync(ctx, detail, ct);
        return ok ? (1, 0, 0) : (0, 1, 0);
    }

    public static async Task<bool> DownloadOneAsync(JobContext ctx, AwemeDetail a, CancellationToken ct)
    {
        var author = UrlParser.SanitizeFilename(string.IsNullOrWhiteSpace(a.AuthorName) ? "unknown" : a.AuthorName);
        var authorDir = Path.Combine(ctx.RootDir, author);
        Directory.CreateDirectory(authorDir);
        var tmpl = NameTemplates.Validate(ctx.Settings.Current.FileTemplate);
        var modeName = ctx.Row.Mode.ToString().ToLowerInvariant();
        var nameCtx = NameTemplates.BuildAwemeContext(a, modeName);
        var itemName = NameTemplates.Render(tmpl, nameCtx);
        var dir = Path.Combine(authorDir, itemName);
        Directory.CreateDirectory(dir);

        var isGallery = a.Images.Count > 0;
        if (isGallery)
        {
            int i = 0;
            foreach (var img in a.Images)
            {
                var url = img.BestUrl;
                if (string.IsNullOrEmpty(url)) continue;
                var ext = GuessImageExt(url);
                var fp = Path.Combine(dir, $"{i:D3}{ext}");
                ctx.Log?.Invoke($"图 {i}: {url}");
                var r = await ctx.Media.DownloadAsync(url, fp, ct);
                if (!r.Success) return false;
                i++;
            }
            if (i == 0) return false;
        }
        else
        {
            var url = !string.IsNullOrEmpty(a.BestNoWatermarkUrl) ? a.BestNoWatermarkUrl : (a.BestVideoUrl ?? a.VideoUrls.FirstOrDefault() ?? "");
            if (string.IsNullOrEmpty(url))
            {
                ctx.Log?.Invoke($"无视频 URL: {a.AwemeId}");
                return false;
            }
            var ext = url.Contains(".mp4") ? ".mp4" : ".mp4";
            var fp = Path.Combine(dir, itemName + ext);
            ctx.Log?.Invoke($"视频: {url}");
            var r = await ctx.Media.DownloadAsync(url, fp, ct);
            if (!r.Success) return false;
        }

        // 元数据 JSON
        try
        {
            var json = JsonSerializer.Serialize(a, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(dir, itemName + "_data.json"), json, ct);
        }
        catch { /* ignore */ }

        if (ctx.Settings.Current.Database)
        {
            ctx.Db.Upsert(new AwemeRecord
            {
                AwemeId = a.AwemeId,
                AwemeType = isGallery ? "image" : "video",
                Title = a.Title,
                AuthorId = a.AuthorId,
                AuthorName = a.AuthorName,
                SecUid = a.AuthorSecUid,
                CreateTime = a.CreateTime,
                FilePath = dir,
                Mode = ctx.Row.Mode.ToString().ToLowerInvariant(),
            });
        }
        return true;
    }

    private static string GuessImageExt(string url)
    {
        var u = url.ToLowerInvariant();
        if (u.Contains(".png")) return ".png";
        if (u.Contains(".webp")) return ".webp";
        if (u.Contains(".heic")) return ".heic";
        return ".jpg";
    }
}

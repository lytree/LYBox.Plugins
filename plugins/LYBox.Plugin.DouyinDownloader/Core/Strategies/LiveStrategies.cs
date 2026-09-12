using System.Text.Json;
using LYBox.Plugin.DouyinDownloader.Models;
using LYBox.Plugin.DouyinDownloader.Storage;
using LYBox.Plugin.DouyinDownloader.Utils;

namespace LYBox.Plugin.DouyinDownloader.Core.Strategies;

/// <summary>直播录制（FLV 优先,HLS 仅保存 playlist）。支持进度回调 + Pause/Resume。</summary>
public sealed class LiveStrategy : IDownloadStrategy
{
    private readonly LiveSessionRegistry _registry;

    public LiveStrategy(LiveSessionRegistry registry) { _registry = registry; }

    public UrlKind[] SupportedKinds => new[] { UrlKind.Live };
    public DownloadMode Mode => DownloadMode.Post;

    public async Task<(int Success, int Failed, int Skipped)> ExecuteAsync(JobContext ctx, CancellationToken ct)
    {
        var roomId = ctx.Parsed.RoomId!;
        await ctx.RateLimiter.AcquireAsync(ct);
        var info = await ctx.Api.GetLiveRoomInfoAsync(roomId, ctx.Parsed.SecUserId, ctx.Parsed.RoomIdKind ?? "web_rid", ct);
        if (info == null)
        {
            ctx.Log?.Invoke($"直播间信息获取失败: {roomId}");
            return (0, 1, 0);
        }
        if (info.Status != 2)
        {
            ctx.Log?.Invoke($"主播未开播 (status={info.Status}): {roomId}");
            return (0, 0, 1);
        }
        if (string.IsNullOrEmpty(info.StreamUrl))
        {
            ctx.Log?.Invoke($"无播放地址: {roomId}");
            return (0, 1, 0);
        }
        var author = UrlParser.SanitizeFilename(string.IsNullOrWhiteSpace(info.AuthorName) ? "unknown" : info.AuthorName);
        var authorDir = Path.Combine(ctx.RootDir, author);
        Directory.CreateDirectory(authorDir);
        var tmpl = NameTemplates.Validate(ctx.Settings.Current.FileTemplate);
        var nameCtx = NameTemplates.BuildContext(roomId, info.Title, info.AuthorName, info.AuthorSecUid, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), "live", "live");
        var stem = NameTemplates.Render(tmpl, nameCtx);
        var dir = Path.Combine(authorDir, stem);
        Directory.CreateDirectory(dir);

        try
        {
            var json = JsonSerializer.Serialize(info.Raw, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(dir, stem + "_room.json"), json, ct);
        }
        catch { }

        var ext = info.IsHls ? ".m3u8" : ".flv";
        var target = Path.Combine(dir, stem + ext);
        if (info.IsHls)
        {
            ctx.Log?.Invoke($"流为 HLS,只保存 playlist。如需可播放文件请用 ffmpeg: ffmpeg -i \"{info.StreamUrl}\" -c copy out.ts");
        }

        var recorder = _registry.Recorder;
        var resumeKey = $"live:{roomId}:{target}";

        // 注册会话 → Orchestrator 可暂停/恢复
        var session = new LiveSessionRegistry.Session
        {
            JobId = ctx.Row.JobId,
            RoomId = roomId,
            StreamUrl = info.StreamUrl,
            TargetPath = target,
            ResumeKey = resumeKey,
            Recorder = recorder,
            WasHls = info.IsHls,
        };
        _registry.Register(session);

        var options = new LiveRecorder.LiveOptions
        {
            MaxDurationSeconds = ctx.Settings.Current.LiveMaxDurationSeconds,
            ChunkSize = ctx.Settings.Current.LiveChunkSize,
            IdleTimeoutSeconds = ctx.Settings.Current.LiveIdleTimeoutSeconds,
            ResumeKey = resumeKey,
            OnProgress = (bytesDelta, elapsed, idle) =>
            {
                _registry.Update(ctx.Row.JobId, s =>
                {
                    s.Bytes += bytesDelta;
                    s.ElapsedSec = elapsed;
                    s.IdleSec = idle;
                    s.StatusText = $"录制中 {FormatBytes(s.Bytes)} / {elapsed:F0}s / 空闲 {idle:F1}s";
                    ctx.Row.StatusText = s.StatusText;
                });
            },
            OnPauseChanged = (paused, _) =>
            {
                _registry.Update(ctx.Row.JobId, s => { s.Paused = paused; s.Status = paused ? JobStatus.Paused : JobStatus.Running; });
                ctx.Row.StatusText = paused ? "已暂停 (已保存已录字节)" : "已恢复录制";
            },
        };

        var result = await recorder.RecordAsync(info.StreamUrl, target, options, ct);
        _registry.Unregister(ctx.Row.JobId);

        if (result.Paused)
        {
            ctx.Log?.Invoke($"录制已暂停 ({result.StopReason}): 已写 {FormatBytes(result.Bytes)}");
            ctx.Row.Status = JobStatus.Paused;
            ctx.Row.StatusText = "已暂停";
            ctx.Row.FinishedAt = null;
            return (0, 0, 0);
        }
        if (!result.Success)
        {
            ctx.Log?.Invoke($"录制失败 ({result.StopReason}): {roomId}");
            return (0, 1, 0);
        }

        if (ctx.Settings.Current.Database)
        {
            ctx.Db.Upsert(new AwemeRecord
            {
                AwemeId = roomId,
                AwemeType = "live",
                Title = info.Title,
                AuthorId = info.AuthorSecUid,
                AuthorName = info.AuthorName,
                SecUid = info.AuthorSecUid,
                CreateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                FilePath = dir,
                Mode = "live",
            });
        }
        return (1, 0, 0);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:F1} MB";
        return $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
    }
}

/// <summary>直播回放（与 live_replay_downloader.py 等价：先拉 episode 拿 room_id,再拉 replay_list 拿 URL）。</summary>
public sealed class LiveReplayStrategy : IDownloadStrategy
{
    public UrlKind[] SupportedKinds => new[] { UrlKind.LiveReplay };
    public DownloadMode Mode => DownloadMode.Post;

    public async Task<(int Success, int Failed, int Skipped)> ExecuteAsync(JobContext ctx, CancellationToken ct)
    {
        var episodeId = ctx.Parsed.EpisodeId!;
        await ctx.RateLimiter.AcquireAsync(ct);
        var info = await ctx.Api.GetLiveReplayInfoAsync(episodeId, ctx.Parsed.ReplayId, ct);
        if (info == null)
        {
            ctx.Log?.Invoke($"回放信息获取失败: {episodeId}");
            return (0, 1, 0);
        }
        if (string.IsNullOrEmpty(info.VideoUrl))
        {
            ctx.Log?.Invoke($"无回放视频 URL: {episodeId}");
            return (0, 1, 0);
        }
        var author = UrlParser.SanitizeFilename(string.IsNullOrWhiteSpace(info.AuthorName) ? "unknown" : info.AuthorName);
        var authorDir = Path.Combine(ctx.RootDir, author);
        Directory.CreateDirectory(authorDir);
        var tmpl = NameTemplates.Validate(ctx.Settings.Current.FileTemplate);
        var nameCtx = NameTemplates.BuildContext(episodeId, info.Title, info.AuthorName, "", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), "live_replay", "live_replay");
        var stem = NameTemplates.Render(tmpl, nameCtx);
        var dir = Path.Combine(authorDir, stem);
        Directory.CreateDirectory(dir);

        var videoExt = info.VideoUrl.Contains(".mp4") ? ".mp4" : ".mp4";
        var videoPath = Path.Combine(dir, stem + "_video" + videoExt);
        var ok = true;
        var r1 = await ctx.Media.DownloadAsync(info.VideoUrl, videoPath, ct);
        if (!r1.Success) { ok = false; ctx.Log?.Invoke($"视频轨下载失败: {info.VideoUrl}"); }

        if (!string.IsNullOrEmpty(info.AudioUrl))
        {
            var audioPath = Path.Combine(dir, stem + "_audio.mp4");
            var r2 = await ctx.Media.DownloadAsync(info.AudioUrl, audioPath, ct);
            if (!r2.Success) ctx.Log?.Invoke($"音轨下载失败 (保留视频轨): {info.AudioUrl}");
        }

        if (!ok) return (0, 1, 0);

        if (ctx.Settings.Current.Database)
        {
            ctx.Db.Upsert(new AwemeRecord
            {
                AwemeId = episodeId,
                AwemeType = "live_replay",
                Title = info.Title,
                AuthorName = info.AuthorName,
                CreateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                FilePath = dir,
                Mode = "live_replay",
            });
        }
        return (1, 0, 0);
    }
}

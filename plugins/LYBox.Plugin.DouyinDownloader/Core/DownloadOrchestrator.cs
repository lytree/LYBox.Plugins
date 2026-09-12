using LYBox.Plugin.DouyinDownloader.Config;
using LYBox.Plugin.DouyinDownloader.Control;
using LYBox.Plugin.DouyinDownloader.Core.Strategies;
using LYBox.Plugin.DouyinDownloader.Models;
using LYBox.Plugin.DouyinDownloader.Storage;
using LYBox.Plugin.DouyinDownloader.Utils;

namespace LYBox.Plugin.DouyinDownloader.Core;

/// <summary>下载总调度：消费队列 → 选择策略 → 调用 RateLimiter/Retry → 更新 JobRow。</summary>
public sealed class DownloadOrchestrator : IHostedServiceLite
{
    private readonly DownloadQueue _queue;
    private readonly DownloaderFactory _factory;
    private readonly DouyinApiClient _api;
    private readonly MediaDownloader _media;
    private readonly DownloadDatabase _db;
    private readonly RateLimiter _rateLimiter;
    private readonly DownloaderSettingsStore _settings;
    private readonly LiveSessionRegistry _liveRegistry;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public DownloadOrchestrator(
        DownloadQueue queue, DownloaderFactory factory, DouyinApiClient api,
        MediaDownloader media, DownloadDatabase db, RateLimiter rateLimiter,
        DownloaderSettingsStore settings, LiveSessionRegistry liveRegistry)
    {
        _queue = queue;
        _factory = factory;
        _api = api;
        _media = media;
        _db = db;
        _rateLimiter = rateLimiter;
        _settings = settings;
        _liveRegistry = liveRegistry;
    }

    public LiveSessionRegistry LiveRegistry => _liveRegistry;

    public void Start()
    {
        if (_loop != null) return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        try { if (_loop != null) await _loop; } catch { }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var sem = new SemaphoreSlim(Math.Max(1, _settings.Current.Concurrency));
        try
        {
            await foreach (var row in _queue.ReadAllAsync(ct))
            {
                await sem.WaitAsync(ct);
                _ = Task.Run(async () =>
                {
                    try { await ExecuteOneAsync(row, ct); }
                    finally { sem.Release(); }
                }, ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    public async Task ExecuteOneAsync(DownloadJobRow row, CancellationToken ct)
    {
        row.Status = JobStatus.Running;
        row.StartedAt = DateTimeOffset.Now;
        row.StatusText = "运行中";
        _queue.Update(row.JobId, _ => { });
        var url = row.Url;
        // 解析短链
        if (UrlParser.IsShortUrl(url))
        {
            var resolved = await _api.ResolveShortUrlAsync(url, ct);
            if (!string.IsNullOrEmpty(resolved)) url = resolved;
        }
        var parsed = UrlParser.Parse(url);
        if (parsed == null || parsed.Kind == UrlKind.Unknown || parsed.Kind == UrlKind.Short)
        {
            row.Status = JobStatus.Failed;
            row.LastError = "URL 不支持或解析失败";
            row.StatusText = row.LastError;
            row.FinishedAt = DateTimeOffset.Now;
            return;
        }

        var root = ResolveRootDir(row);
        row.OutputDir = root;

        var ctx = new JobContext(
            RootDir: root,
            Row: row,
            Parsed: parsed,
            Settings: _settings,
            Api: _api,
            Media: _media,
            Db: _db,
            RateLimiter: _rateLimiter,
            Log: msg => Logger.Info($"[Job {row.JobId}] {msg}"));

        var strategy = _factory.ResolveOrFallback(parsed.Kind, row.Mode);
        try
        {
            var (s, f, sk) = await strategy.ExecuteAsync(ctx, ct);
            row.Success = s; row.Failed = f; row.Skipped = sk;
            row.Total = s + f + sk;

            // 仅 1 页结果 + 用户期望 >0 + 失败 = 0 时 → 升级到浏览器兜底
            if (parsed.Kind == UrlKind.User
                && row.Mode == DownloadMode.Post
                && _factory.BrowserFallback != null
                && row.Total <= 20
                && row.Number > 20
                && f == 0)
            {
                ctx.Log?.Invoke($"API 仅返回 {row.Total} 个作品,自动升级到浏览器兜底");
                var browser = _factory.BrowserFallback;
                var (s2, f2, sk2) = await browser.ExecuteAsync(ctx, ct);
                row.Success += s2; row.Failed += f2; row.Skipped += sk2;
                row.Total = row.Success + row.Failed + row.Skipped;
            }

            row.Status = (f == 0 && sk == s + f + sk) ? JobStatus.Skipped : JobStatus.Success;
            row.StatusText = $"完成 (成功 {s} / 失败 {f} / 跳过 {sk})";
            row.FinishedAt = DateTimeOffset.Now;
        }
        catch (LoginRequiredException ex)
        {
            row.Status = JobStatus.Failed;
            row.LastError = "需要登录: " + ex.Message;
            row.StatusText = row.LastError;
            row.FinishedAt = DateTimeOffset.Now;
        }
        catch (Exception ex)
        {
            row.Status = JobStatus.Failed;
            row.LastError = ex.Message;
            row.StatusText = "失败: " + ex.Message;
            row.FinishedAt = DateTimeOffset.Now;
        }
    }

    private string ResolveRootDir(DownloadJobRow row)
    {
        var root = _settings.Current.DownloadPath;
        if (!Path.IsPathRooted(root))
            root = Path.Combine(AppContext.BaseDirectory, root);
        Directory.CreateDirectory(root);
        var modeName = row.Mode.ToString().ToLowerInvariant();
        var sub = Path.Combine(root, modeName);
        Directory.CreateDirectory(sub);
        return sub;
    }

    /// <summary>请求暂停指定任务。Live 类型会立刻停止读取并 promote 已录字节;其他类型仅标记状态。</summary>
    public bool Pause(Guid jobId)
    {
        var row = _queue.Get(jobId);
        if (row == null) return false;
        var session = _liveRegistry.Get(jobId);
        if (session != null)
        {
            session.Recorder.Pause(session.ResumeKey);
            return true;
        }
        // 非 Live: 直接标记 Paused（不会真正中断正在运行的下载线程,仅 UI 状态）
        row.Status = JobStatus.Paused;
        row.StatusText = "已暂停";
        return true;
    }

    /// <summary>恢复 Live 录制:重新拉流并 append 到现有 .tmp。</summary>
    public async Task<LiveRecorder.LiveResult?> ResumeAsync(Guid jobId)
    {
        var session = _liveRegistry.Get(jobId);
        if (session == null) return null;
        // 重置暂停标记,允许新任务执行
        session.Recorder.Pause(session.ResumeKey); // ensure state
        session.Status = JobStatus.Running;
        session.Paused = false;
        return await session.Recorder.ResumeAsync(session.StreamUrl, session.TargetPath,
            new LiveRecorder.LiveOptions
            {
                MaxDurationSeconds = _settings.Current.LiveMaxDurationSeconds,
                ChunkSize = _settings.Current.LiveChunkSize,
                IdleTimeoutSeconds = _settings.Current.LiveIdleTimeoutSeconds,
                ResumeKey = session.ResumeKey,
            });
    }
}

public interface IHostedServiceLite
{
    void Start();
    Task StopAsync();
}

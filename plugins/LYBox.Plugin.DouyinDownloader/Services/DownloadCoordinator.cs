using LYBox.Plugin.DouyinDownloader.Config;
using LYBox.Plugin.DouyinDownloader.Control;
using LYBox.Plugin.DouyinDownloader.Core;
using LYBox.Plugin.DouyinDownloader.Models;
using LYBox.Plugin.DouyinDownloader.Utils;

namespace LYBox.Plugin.DouyinDownloader.Services;

/// <summary>
/// 顶层协调器（页面/Web 都通过它提交/查询）。
/// 自动解析短链,选择策略,入队并触发调度。
/// </summary>
public sealed class DownloadCoordinator
{
    private readonly DownloadQueue _queue;
    private readonly DouyinApiClient _api;
    private readonly DownloadOrchestrator _orchestrator;
    private readonly DownloaderSettingsStore _settings;

    public DownloadCoordinator(DownloadQueue queue, DouyinApiClient api, DownloadOrchestrator orchestrator, DownloaderSettingsStore settings)
    {
        _queue = queue;
        _api = api;
        _orchestrator = orchestrator;
        _settings = settings;
    }

    public DownloadOrchestrator Orchestrator => _orchestrator;

    public DownloaderSettingsStore Settings => _settings;
    public DownloadQueue Queue => _queue;
    public DouyinApiClient Api => _api;

    public async Task<(bool ok, string? message, Guid? jobId)> SubmitAsync(string url, DownloadMode mode = DownloadMode.Post, int number = 0, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null)
    {
        if (string.IsNullOrEmpty(_settings.Current.DownloadPath))
            return (false, "请先在设置中配置下载目录", null);

        var resolved = url;
        if (UrlParser.IsShortUrl(url))
        {
            var r = await _api.ResolveShortUrlAsync(url);
            if (!string.IsNullOrEmpty(r)) resolved = r;
        }

        var parsed = UrlParser.Parse(resolved);
        if (parsed == null) return (false, "URL 解析失败", null);

        var row = new DownloadJobRow
        {
            Url = url,
            UrlKind = parsed.Kind,
            Mode = mode,
            Status = JobStatus.Queued,
            StatusText = "已入队",
        };
        if (!_queue.TryEnqueue(row, out var dup))
            return (false, dup ?? "入队失败", null);

        // 启动调度
        _orchestrator.Start();
        return (true, null, row.JobId);
    }

    public IEnumerable<DownloadJobRow> ListJobs() => _queue.All();
    public DownloadJobRow? GetJob(Guid id) => _queue.Get(id);

    public bool Remove(Guid id) => _queue.Remove(id);

    public bool Pause(Guid jobId) => _orchestrator.Pause(jobId);

    public Task<LiveRecorder.LiveResult?> ResumeAsync(Guid jobId) => _orchestrator.ResumeAsync(jobId);
}

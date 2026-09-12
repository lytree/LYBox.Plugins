using LYBox.Plugin.DouyinDownloader.Config;
using LYBox.Plugin.DouyinDownloader.Models;

namespace LYBox.Plugin.DouyinDownloader.Core;

/// <summary>正在进行的直播录制会话 (jobId → session)。供 Pause/Resume 跨策略调用。</summary>
public sealed class LiveSessionRegistry
{
    public sealed class Session
    {
        public required Guid JobId { get; init; }
        public required string RoomId { get; init; }
        public required string StreamUrl { get; init; }
        public required string TargetPath { get; init; }
        public required string ResumeKey { get; init; }
        public required LiveRecorder Recorder { get; init; }
        public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.Now;
        public long Bytes { get; set; }
        public double ElapsedSec { get; set; }
        public double IdleSec { get; set; }
        public JobStatus Status { get; set; } = JobStatus.Running;
        public string? StatusText { get; set; }
        public bool WasHls { get; set; }
        public bool Paused { get; set; }
    }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, Session> _sessions = new();
    private readonly DownloaderSettingsStore _settings;

    public LiveSessionRegistry(DownloaderSettingsStore settings) { _settings = settings; }

    public LiveRecorder Recorder => new(_settings);

    public void Register(Session s) => _sessions[s.JobId] = s;
    public void Unregister(Guid jobId) => _sessions.TryRemove(jobId, out _);
    public Session? Get(Guid jobId) => _sessions.TryGetValue(jobId, out var s) ? s : null;
    public IEnumerable<Session> All() => _sessions.Values.OrderBy(s => s.StartedAt);
    public void Update(Guid jobId, Action<Session> update)
    {
        if (_sessions.TryGetValue(jobId, out var s)) update(s);
    }
}

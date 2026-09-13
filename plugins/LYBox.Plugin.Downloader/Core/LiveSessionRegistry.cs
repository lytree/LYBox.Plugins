using LYBox.Plugin.Downloader.Config;
using LYBox.Plugin.Downloader.Models;

namespace LYBox.Plugin.Downloader.Core;

/// <summary>正在进行的直播录制会话 (jobId → session)。供 Pause/Resume 跨策略调用。</summary>
public sealed class LiveSessionRegistry
{
    public sealed class Session
    {
        public required Guid JobId { get; init; }
        public required string StreamUrl { get; init; }
        public required string TargetPath { get; init; }
        public required string ResumeKey { get; init; }
        public required LiveRecorder Recorder { get; init; }
        public long Bytes { get; set; }
    }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, Session> _sessions = new();
    private readonly DouyinSettingsStore _settings;

    public LiveSessionRegistry(DouyinSettingsStore settings) { _settings = settings; }

    public LiveRecorder Recorder => new(_settings);

    public void Register(Session s) => _sessions[s.JobId] = s;
    public void Unregister(Guid jobId) => _sessions.TryRemove(jobId, out _);
    public Session? Get(Guid jobId) => _sessions.TryGetValue(jobId, out var s) ? s : null;
    public void Update(Guid jobId, Action<Session> update)
    {
        if (_sessions.TryGetValue(jobId, out var s)) update(s);
    }
}

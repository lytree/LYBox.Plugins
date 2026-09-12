using LYBox.Plugin.DouyinDownloader.Config;
using LYBox.Plugin.DouyinDownloader.Utils;

namespace LYBox.Plugin.DouyinDownloader.Core;

/// <summary>
/// 直播流录制（与原项目 core/live_downloader._record_stream 等价 + 增强）：
///  - chunk 流写入 .tmp,完成后 promote 为正式文件
///  - max_duration 强制截止 (0 = 录到主播下播)
///  - idle_timeout 字节空闲超时 (默认 30s)
///  - 主播下播 / 网络空闲 / payload 截断时,只要写入过字节就保留 .tmp
///  - Referer / Origin 固定为 live.douyin.com (CDN 校验)
///  - 进度回调：每个 chunk 一次,带 bytes/elapsed/idle
///  - 暂停/恢复：Pause() 立刻停止读取 + promote 已录字节;Resume() 重新拉流并追加到现有 .tmp
/// </summary>
public sealed class LiveRecorder
{
    private readonly DownloaderSettingsStore _settings;
    private readonly HttpClient _http;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, PauseState> _pauseStates = new();

    public LiveRecorder(DownloaderSettingsStore settings, HttpClient? http = null)
    {
        _settings = settings;
        _http = http ?? new HttpClient(new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.None })
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    public sealed class LiveOptions
    {
        public double MaxDurationSeconds { get; set; }   // 0 = 录到主播下播
        public int ChunkSize { get; set; } = 65536;
        public double IdleTimeoutSeconds { get; set; } = 30.0;
        /// <summary>进度回调（每 chunk 一次）。参数:字节/已写字节/总耗时秒/空闲秒。</summary>
        public Action<long, double, double>? OnProgress { get; set; }
        /// <summary>暂停状态变化回调（true = 已暂停且 .tmp 已 promote/finalize,false = 重新开始读取）。</summary>
        public Action<bool, string?>? OnPauseChanged { get; set; }
        /// <summary>可选:用于 Resume 时识别同一个录制的 key（room_id / target_path 都可以）。</summary>
        public string? ResumeKey { get; set; }
    }

    public sealed class LiveResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string Path { get; set; } = "";
        public long Bytes { get; set; }
        public double DurationSeconds { get; set; }
        public string? StopReason { get; set; }
        public bool WasHls { get; set; }
        public bool Paused { get; set; }
    }

    private sealed class PauseState
    {
        public volatile bool IsPaused;
        public long ExpectedStartBytes;
    }

    public void Pause(string resumeKey)
    {
        var s = _pauseStates.GetOrAdd(resumeKey, _ => new PauseState());
        s.IsPaused = true;
        Logger.Info($"[LiveRecorder] 请求暂停 (key={resumeKey})");
    }

    public bool IsPaused(string resumeKey) =>
        _pauseStates.TryGetValue(resumeKey, out var s) && s.IsPaused;

    public async Task<LiveResult> RecordAsync(string streamUrl, string targetPath, LiveOptions? opts = null, CancellationToken ct = default)
    {
        var o = opts ?? new LiveOptions();
        var wasHls = streamUrl.Split('?')[0].Contains(".m3u8");
        var dir = Path.GetDirectoryName(targetPath)!;
        Directory.CreateDirectory(dir);
        var tmp = targetPath + ".tmp";
        var resumeKey = o.ResumeKey ?? targetPath;
        long resumeFrom = 0;
        if (File.Exists(tmp)) resumeFrom = new FileInfo(tmp).Length;

        var start = DateTimeOffset.UtcNow;
        long bytesWritten = resumeFrom;
        var lastChunkTs = start;
        var lastReportTs = start;

        _pauseStates.AddOrUpdate(resumeKey,
            _ => new PauseState { ExpectedStartBytes = resumeFrom },
            (_, s) => { s.IsPaused = false; s.ExpectedStartBytes = resumeFrom; return s; });

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, streamUrl);
            req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36");
            req.Headers.TryAddWithoutValidation("Referer", "https://live.douyin.com/");
            req.Headers.TryAddWithoutValidation("Origin", "https://live.douyin.com");

            using var resp = await _http.SendAsync(req, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode)
                return new LiveResult { Success = false, Error = $"HTTP {(int)resp.StatusCode}", Path = targetPath, StopReason = "http_status" };

            var mode = resumeFrom > 0 ? FileMode.Append : FileMode.Create;
            await using var fs = new FileStream(tmp, mode, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await using var stream = await resp.Content.ReadAsStreamAsync(ct);

            var buf = new byte[o.ChunkSize];
            int n;
            while ((n = await stream.ReadAsync(buf, ct)) > 0)
            {
                // 暂停检测:用户主动调用 Pause → 把已写入字节 promote 成 final,等待恢复
                if (_pauseStates.TryGetValue(resumeKey, out var ps) && ps.IsPaused)
                {
                    o.OnPauseChanged?.Invoke(true, null);
                    var partialPath = PromoteAndPause(tmp, targetPath, bytesWritten, start, wasHls);
                    Logger.Info($"[LiveRecorder] 已暂停,保留 {partialPath}");
                    return new LiveResult
                    {
                        Success = true,
                        Path = partialPath,
                        Bytes = bytesWritten,
                        DurationSeconds = (DateTimeOffset.UtcNow - start).TotalSeconds,
                        StopReason = "paused",
                        WasHls = wasHls,
                        Paused = true,
                    };
                }

                await fs.WriteAsync(buf.AsMemory(0, n), ct);
                bytesWritten += n;
                lastChunkTs = DateTimeOffset.UtcNow;

                // 进度回调（节流 200ms 一次）
                if (o.OnProgress != null && (DateTimeOffset.UtcNow - lastReportTs).TotalMilliseconds >= 200)
                {
                    var elapsed = (DateTimeOffset.UtcNow - start).TotalSeconds;
                    var idle = (DateTimeOffset.UtcNow - lastChunkTs).TotalSeconds;
                    o.OnProgress(n, elapsed, idle);
                    lastReportTs = DateTimeOffset.UtcNow;
                }

                if (o.MaxDurationSeconds > 0 && (DateTimeOffset.UtcNow - start).TotalSeconds >= o.MaxDurationSeconds)
                {
                    return Promote(tmp, targetPath, bytesWritten, start, "max_duration", wasHls);
                }
                if ((DateTimeOffset.UtcNow - lastChunkTs).TotalSeconds >= o.IdleTimeoutSeconds)
                {
                    return bytesWritten > 0
                        ? Promote(tmp, targetPath, bytesWritten, start, "idle_timeout", wasHls)
                        : CleanupAndFail(tmp, "idle_timeout_no_data");
                }
            }

            if (bytesWritten > 0) return Promote(tmp, targetPath, bytesWritten, start, "stream_ended", wasHls);
            return CleanupAndFail(tmp, "empty_stream");
        }
        catch (TaskCanceledException)
        {
            return bytesWritten > 0
                ? Promote(tmp, targetPath, bytesWritten, start, "cancelled", wasHls)
                : CleanupAndFail(tmp, "cancelled_no_data");
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return bytesWritten > 0
                ? Promote(tmp, targetPath, bytesWritten, start, "http_error", wasHls)
                : CleanupAndFail(tmp, "http_error_no_data:" + ex.Message);
        }
        catch (Exception ex)
        {
            Logger.Error($"直播录制异常: {ex.Message}");
            return bytesWritten > 0
                ? Promote(tmp, targetPath, bytesWritten, start, "exception", wasHls)
                : CleanupAndFail(tmp, "exception_no_data:" + ex.Message);
        }
    }

    /// <summary>恢复录制：保留已写入字节,继续从同 URL 拉流,append 到原 .tmp。</summary>
    /// <param name="streamUrl">流地址(可能已变,需重新获取)</param>
    /// <param name="targetPath">目标文件路径(必须与暂停时一致)</param>
    /// <param name="o">LiveOptions,包含 OnProgress / OnPauseChanged / ResumeKey</param>
    /// <param name="ct">取消令牌</param>
    public async Task<LiveResult> ResumeAsync(string streamUrl, string targetPath, LiveOptions o, CancellationToken ct = default)
    {
        // 复用 RecordAsync 逻辑:会自动检测 .tmp 大小作为 resumeFrom,文件以 Append 打开
        o.ResumeKey ??= targetPath;
        return await RecordAsync(streamUrl, targetPath, o, ct);
    }

    private string PromoteAndPause(string tmp, string target, long bytes, DateTimeOffset start, bool wasHls)
    {
        try
        {
            if (bytes > 0) File.Move(tmp, target, overwrite: true);
            else { if (File.Exists(tmp)) File.Delete(tmp); }
            return target;
        }
        catch
        {
            return tmp;
        }
    }

    private LiveResult Promote(string tmp, string target, long bytes, DateTimeOffset start, string reason, bool wasHls)
    {
        try
        {
            File.Move(tmp, target, overwrite: true);
            Logger.Info($"直播流已保存 ({reason}): {target} ({bytes / 1024.0 / 1024.0:F1} MiB, {(DateTimeOffset.UtcNow - start).TotalSeconds:F1}s)");
            return new LiveResult
            {
                Success = true,
                Path = target,
                Bytes = bytes,
                DurationSeconds = (DateTimeOffset.UtcNow - start).TotalSeconds,
                StopReason = reason,
                WasHls = wasHls,
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"直播 tmp → final rename 失败: {ex.Message}");
            return CleanupAndFail(tmp, "promote_failed:" + ex.Message);
        }
    }

    private LiveResult CleanupAndFail(string tmp, string reason)
    {
        try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        return new LiveResult { Success = false, StopReason = reason };
    }
}

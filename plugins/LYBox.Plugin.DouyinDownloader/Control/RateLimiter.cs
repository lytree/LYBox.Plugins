namespace LYBox.Plugin.DouyinDownloader.Control;

/// <summary>简单令牌桶限流器（默认 2 请求/秒）。</summary>
public sealed class RateLimiter : IDisposable
{
    private readonly double _rps;
    private double _tokens;
    private readonly object _lock = new();
    private DateTime _lastRefill = DateTime.UtcNow;
    private readonly CancellationTokenSource _cts = new();

    public RateLimiter(double rps = 2.0)
    {
        _rps = Math.Max(0.1, rps);
        _tokens = _rps;
    }

    public async Task AcquireAsync(CancellationToken ct = default)
    {
        while (!_cts.IsCancellationRequested)
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var elapsed = (now - _lastRefill).TotalSeconds;
                _tokens = Math.Min(_rps, _tokens + elapsed * _rps);
                _lastRefill = now;
                if (_tokens >= 1.0)
                {
                    _tokens -= 1.0;
                    return;
                }
            }
            var waitMs = (int)((1.0 - _tokens) / _rps * 1000);
            try { await Task.Delay(waitMs, ct); }
            catch (TaskCanceledException) { return; }
        }
    }

    public void Dispose() => _cts.Cancel();
}

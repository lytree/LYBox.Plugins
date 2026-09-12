using LYBox.Plugin.DouyinDownloader.Utils;

namespace LYBox.Plugin.DouyinDownloader.Control;

/// <summary>指数退避重试（1s, 2s, 5s）。</summary>
public sealed class RetryHandler
{
    private static readonly int[] DefaultDelays = { 1, 2, 5 };

    public async Task<T> ExecuteAsync<T>(Func<int, CancellationToken, Task<T>> action, int maxRetries = 3, CancellationToken ct = default)
    {
        Exception? last = null;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try { return await action(attempt, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                last = ex;
                if (attempt == maxRetries - 1) break;
                var delay = DefaultDelays[Math.Min(attempt, DefaultDelays.Length - 1)];
                Logger.Warn($"重试 ({attempt + 1}/{maxRetries}) 等待 {delay}s: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(delay), ct);
            }
        }
        throw last ?? new InvalidOperationException("retry exhausted");
    }
}

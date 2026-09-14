using System.Collections.Concurrent;
using System.Threading.Channels;
using LYBox.Plugin.Downloader.Models;

namespace LYBox.Plugin.Downloader.Control;

/// <summary>下载任务队列（Channel + 状态表）。</summary>
public sealed class DownloadQueue
{
    private readonly Channel<DownloadJobRow> _channel = Channel.CreateUnbounded<DownloadJobRow>();
    private readonly ConcurrentDictionary<Guid, DownloadJobRow> _rows = new();
    private readonly HashSet<string> _urls = new(StringComparer.Ordinal);

    public bool TryEnqueue(DownloadJobRow row, out string? duplicateReason)
    {
        duplicateReason = null;
        lock (_urls)
        {
            if (!_urls.Add(row.Url))
            {
                duplicateReason = "已在队列";
                return false;
            }
        }
        if (!_rows.TryAdd(row.JobId, row)) { duplicateReason = "JobId 重复"; return false; }
        _channel.Writer.TryWrite(row);
        return true;
    }

    public IAsyncEnumerable<DownloadJobRow> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);

    public DownloadJobRow? Get(Guid id) => _rows.TryGetValue(id, out var r) ? r : null;
    public IEnumerable<DownloadJobRow> All() => _rows.Values.OrderByDescending(r => r.CreatedAt);

    public bool Remove(Guid id)
    {
        if (!_rows.TryRemove(id, out var r)) return false;
        // 释放 URL 占位,使同一 URL 重新入队不会被误判重复
        lock (_urls) _urls.Remove(r.Url);
        return true;
    }
}

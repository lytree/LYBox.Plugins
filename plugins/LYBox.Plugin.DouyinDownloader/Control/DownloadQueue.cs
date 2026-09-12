using System.Collections.Concurrent;
using System.Threading.Channels;
using LYBox.Plugin.DouyinDownloader.Models;

namespace LYBox.Plugin.DouyinDownloader.Control;

/// <summary>下载任务队列（Channel + 状态表）。</summary>
public sealed class DownloadQueue
{
    private readonly Channel<DownloadJobRow> _channel = Channel.CreateUnbounded<DownloadJobRow>(new() { SingleReader = false });
    private readonly ConcurrentDictionary<Guid, DownloadJobRow> _rows = new();
    private readonly ConcurrentBag<string> _urls = new();

    public int Count => _rows.Count;

    public bool TryEnqueue(DownloadJobRow row, out string? duplicateReason)
    {
        duplicateReason = null;
        if (_urls.Contains(row.Url))
        {
            duplicateReason = "已在队列";
            return false;
        }
        if (!_rows.TryAdd(row.JobId, row)) { duplicateReason = "JobId 重复"; return false; }
        _urls.Add(row.Url);
        _channel.Writer.TryWrite(row);
        return true;
    }

    public IAsyncEnumerable<DownloadJobRow> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);

    public DownloadJobRow? Get(Guid id) => _rows.TryGetValue(id, out var r) ? r : null;
    public IEnumerable<DownloadJobRow> All() => _rows.Values.OrderByDescending(r => r.CreatedAt);

    public void Update(Guid id, Action<DownloadJobRow> update)
    {
        if (_rows.TryGetValue(id, out var r)) update(r);
    }

    public bool Remove(Guid id)
    {
        if (_rows.TryRemove(id, out var r))
        {
            // 不从 ConcurrentBag 中物理移除 (无 Remove) — 仅当任务结束且不再被引用即可
            return true;
        }
        return false;
    }
}

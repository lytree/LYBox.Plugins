using System.Net;
using System.Text;
using System.Text.Json;
using LYBox.Plugin.DouyinDownloader.Config;
using LYBox.Plugin.DouyinDownloader.Models;
using LYBox.Plugin.DouyinDownloader.Services;
using LYBox.Plugin.DouyinDownloader.Utils;

namespace LYBox.Plugin.DouyinDownloader.WebServer;

/// <summary>
/// 内嵌 HTTP 服务（HttpListener，等价于原项目 server/app.py 的精简版）。
/// 路由：
///   GET  /                  — 控制台首页
///   POST /api/v1/download   — 提交 {url, mode, number}
///   GET  /api/v1/jobs       — 任务列表
///   GET  /api/v1/jobs/{id}  — 单任务
///   DELETE /api/v1/jobs/{id}— 移除任务
///   GET  /api/v1/settings   — 获取设置
///   POST /api/v1/settings   — 更新设置
///   GET  /api/v1/health     — 健康探针
/// </summary>
public sealed class WebConsoleServer : IDisposable
{
    private readonly DownloadCoordinator _coord;
    private readonly DownloaderSettingsStore _settings;
    private readonly HttpListener _listener;
    private CancellationTokenSource? _cts;

    public string Host { get; }
    public int Port { get; }
    public bool IsRunning => _listener.IsListening;

    public WebConsoleServer(DownloadCoordinator coord, DownloaderSettingsStore settings)
    {
        _coord = coord;
        _settings = settings;
        Host = _settings.Current.WebConsoleHost;
        Port = _settings.Current.WebConsolePort;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://{Host}:{Port}/");
    }

    public void Start()
    {
        if (!_settings.Current.EnableWebConsole) return;
        if (IsRunning) return;
        try
        {
            _listener.Start();
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => LoopAsync(_cts.Token));
            Logger.Info($"[WebConsole] 启动于 http://{Host}:{Port}/");
        }
        catch (Exception ex)
        {
            Logger.Warn($"[WebConsole] 启动失败: {ex.Message}");
        }
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        try { _listener.Stop(); } catch { }
        _listener.Close();
        await Task.CompletedTask;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
            _ = Task.Run(() => HandleAsync(ctx));
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        try
        {
            var path = ctx.Request.Url?.AbsolutePath ?? "/";
            var method = ctx.Request.HttpMethod;
            Logger.Info($"[WebConsole] {method} {path}");

            if (method == "GET" && path == "/")
            {
                Write(ctx, 200, "text/html; charset=utf-8", IndexHtml);
                return;
            }

            if (method == "GET" && path == "/api/v1/health")
            {
                WriteJson(ctx, 200, new { ok = true, ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
                return;
            }

            if (method == "GET" && path == "/api/v1/jobs")
            {
                var jobs = _coord.ListJobs().Select(j => new
                {
                    job_id = j.JobId,
                    url = j.Url,
                    mode = j.Mode.ToString().ToLowerInvariant(),
                    status = j.Status.ToString(),
                    progress = new { total = j.Total, success = j.Success, failed = j.Failed, skipped = j.Skipped },
                    status_text = j.StatusText,
                    error = j.LastError,
                    created_at = j.CreatedAt,
                    started_at = j.StartedAt,
                    finished_at = j.FinishedAt,
                    output_dir = j.OutputDir,
                });
                WriteJson(ctx, 200, new { jobs });
                return;
            }

            if (method == "GET" && path.StartsWith("/api/v1/jobs/"))
            {
                if (Guid.TryParse(path["/api/v1/jobs/".Length..], out var gid))
                {
                    var job = _coord.GetJob(gid);
                    if (job == null) { WriteJson(ctx, 404, new { error = "not found" }); return; }
                    WriteJson(ctx, 200, job);
                    return;
                }
            }

            if (method == "DELETE" && path.StartsWith("/api/v1/jobs/"))
            {
                if (Guid.TryParse(path["/api/v1/jobs/".Length..], out var gid))
                {
                    var removed = _coord.Remove(gid);
                    WriteJson(ctx, 200, new { removed });
                    return;
                }
            }

            if (method == "POST" && path.StartsWith("/api/v1/jobs/") && path.EndsWith("/pause", StringComparison.OrdinalIgnoreCase))
            {
                if (Guid.TryParse(path["/api/v1/jobs/".Length..][..^"/pause".Length], out var gid))
                {
                    var paused = _coord.Pause(gid);
                    WriteJson(ctx, paused ? 200 : 400, new { paused, job_id = gid });
                    return;
                }
            }

            if (method == "POST" && path.StartsWith("/api/v1/jobs/") && path.EndsWith("/resume", StringComparison.OrdinalIgnoreCase))
            {
                var jobIdStr = path["/api/v1/jobs/".Length..][..^"/resume".Length];
                if (Guid.TryParse(jobIdStr, out var gid))
                {
                    var result = await _coord.ResumeAsync(gid);
                    WriteJson(ctx, result != null ? 200 : 400, new { resumed = result != null, job_id = gid, result });
                    return;
                }
            }

            if (method == "POST" && path == "/api/v1/download")
            {
                var body = await ReadBodyAsync(ctx.Request);
                try
                {
                    var req = JsonSerializer.Deserialize<SubmitRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (req == null || string.IsNullOrWhiteSpace(req.Url))
                    {
                        WriteJson(ctx, 400, new { error = "url is required" });
                        return;
                    }
                    var mode = Enum.TryParse<DownloadMode>(req.Mode, true, out var m) ? m : DownloadMode.Post;
                    var (ok, msg, jobId) = await _coord.SubmitAsync(req.Url, mode, req.Number ?? 0);
                    WriteJson(ctx, ok ? 200 : 400, new { ok, job_id = jobId, message = msg });
                    return;
                }
                catch (Exception ex)
                {
                    WriteJson(ctx, 400, new { error = ex.Message });
                    return;
                }
            }

            if (method == "GET" && path == "/api/v1/settings")
            {
                WriteJson(ctx, 200, _settings.Current);
                return;
            }

            if (method == "POST" && path == "/api/v1/settings")
            {
                var body = await ReadBodyAsync(ctx.Request);
                try
                {
                    var patch = JsonSerializer.Deserialize<DownloaderSettings>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (patch != null)
                    {
                        foreach (var p in typeof(DownloaderSettings).GetProperties())
                            if (p.CanWrite) p.SetValue(_settings.Current, p.GetValue(patch) ?? p.GetValue(_settings.Current));
                        _settings.Save();
                    }
                    WriteJson(ctx, 200, _settings.Current);
                    return;
                }
                catch (Exception ex)
                {
                    WriteJson(ctx, 400, new { error = ex.Message });
                    return;
                }
            }

            WriteJson(ctx, 404, new { error = "not found", path });
        }
        catch (Exception ex)
        {
            try { WriteJson(ctx, 500, new { error = ex.Message }); } catch { }
        }
        finally
        {
            try { ctx.Response.OutputStream.Close(); } catch { }
        }
    }

    private static async Task<string> ReadBodyAsync(HttpListenerRequest req)
    {
        using var sr = new StreamReader(req.InputStream, Encoding.UTF8);
        return await sr.ReadToEndAsync();
    }

    private static void Write(HttpListenerContext ctx, int code, string contentType, string body)
    {
        ctx.Response.StatusCode = code;
        ctx.Response.ContentType = contentType;
        var bytes = Encoding.UTF8.GetBytes(body);
        ctx.Response.ContentLength64 = bytes.Length;
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteJson(HttpListenerContext ctx, int code, object payload)
    {
        ctx.Response.StatusCode = code;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        ctx.Response.ContentLength64 = bytes.Length;
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
    }

    public void Dispose()
    {
        try { StopAsync().GetAwaiter().GetResult(); } catch { }
    }

    private sealed class SubmitRequest
    {
        public string Url { get; set; } = "";
        public string? Mode { get; set; }
        public int? Number { get; set; }
    }

    private const string IndexHtml = """
<!doctype html>
<html lang="zh"><head><meta charset="utf-8"/>
<title>抖音下载器 · Web 控制台</title>
<style>
  body { font-family: -apple-system, "Segoe UI", "Microsoft YaHei", sans-serif; max-width: 980px; margin: 32px auto; padding: 0 16px; color: #1f2328; }
  h1 { font-size: 22px; }
  .row { display: flex; gap: 8px; margin: 12px 0; }
  input, select, button { padding: 6px 10px; font-size: 14px; border: 1px solid #d0d7de; border-radius: 6px; }
  table { width: 100%; border-collapse: collapse; margin-top: 16px; }
  th, td { text-align: left; padding: 8px 6px; border-bottom: 1px solid #eaeef2; font-size: 13px; }
  th { background: #f6f8fa; }
  .status { padding: 2px 8px; border-radius: 4px; background: #f0f0f0; font-size: 12px; }
  .status.Queued { background: #fff8c5; }
  .status.Running { background: #ddf4ff; }
  .status.Success { background: #dafbe1; }
  .status.Failed { background: #ffebe9; color: #cf222e; }
  .status.Skipped { background: #eaeef2; }
  pre { background: #f6f8fa; padding: 12px; border-radius: 6px; overflow: auto; }
</style></head>
<body>
<h1>抖音下载器 · 控制台</h1>
<p>支持单视频 / 图文 / 用户主页 / 合集 / 收藏 / 收藏合集。提交后系统自动调度,完成回调可通过 settings.WebhookUrl 配置。</p>
<div class="row">
  <input id="url" style="flex:1" placeholder="https://www.douyin.com/video/... 或 https://v.douyin.com/..." />
  <select id="mode">
    <option value="post">post (单作品 / 用户主页作品)</option>
    <option value="like">like (用户喜欢)</option>
    <option value="mix">mix (合集)</option>
    <option value="collect">collect (当前账号收藏,仅 self)</option>
    <option value="collectmix">collectmix (收藏合集,仅 self)</option>
  </select>
  <input id="number" type="number" min="0" value="0" style="width:80px" />
  <button onclick="submit()">提交</button>
</div>
<pre id="msg"></pre>
<table><thead><tr><th>JobId</th><th>URL</th><th>Mode</th><th>Status</th><th>Progress</th><th></th></tr></thead>
<tbody id="rows"></tbody></table>
<script>
async function load() {
  const r = await fetch('/api/v1/jobs'); const j = await r.json();
  document.getElementById('rows').innerHTML = (j.jobs||[]).map(x=>`
    <tr>
      <td>${x.job_id.slice(0,8)}</td>
      <td>${(x.url||'').slice(0,80)}</td>
      <td>${x.mode}</td>
      <td><span class="status ${x.status}">${x.status}</span> ${x.status_text||''}</td>
      <td>${x.progress.success}/${x.progress.total} (失败 ${x.progress.failed}, 跳过 ${x.progress.skipped})</td>
      <td><button onclick="pause('${x.job_id}')">暂停</button> <button onclick="resume('${x.job_id}')">恢复</button> <button onclick="del('${x.job_id}')">移除</button></td>
    </tr>`).join('');
}
async function submit() {
  const url = document.getElementById('url').value;
  const mode = document.getElementById('mode').value;
  const number = +document.getElementById('number').value || 0;
  const r = await fetch('/api/v1/download',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({url,mode,number})});
  const j = await r.json();
  document.getElementById('msg').textContent = JSON.stringify(j,null,2);
  load();
}
async function del(id) { await fetch('/api/v1/jobs/'+id,{method:'DELETE'}); load(); }
async function pause(id) { await fetch('/api/v1/jobs/'+id+'/pause',{method:'POST'}); load(); }
async function resume(id) { await fetch('/api/v1/jobs/'+id+'/resume',{method:'POST'}); load(); }
setInterval(load, 3000); load();
</script>
</body></html>
""";
}

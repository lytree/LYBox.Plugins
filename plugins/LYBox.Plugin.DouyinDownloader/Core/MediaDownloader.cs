using System.Net;
using LYBox.Plugin.DouyinDownloader.Config;
using LYBox.Plugin.DouyinDownloader.Utils;

namespace LYBox.Plugin.DouyinDownloader.Core;

/// <summary>通用媒体下载器（HTTP + Range 断点续传 + Content-Length 完整性校验）。</summary>
public sealed class MediaDownloader
{
    private readonly HttpClient _http;
    private readonly DownloaderSettingsStore _settings;

    public MediaDownloader(DownloaderSettingsStore settings, HttpClient? http = null)
    {
        _settings = settings;
        _http = http ?? BuildDefaultHttp(settings.Current.Proxy);
    }

    public async Task<DownloadResult> DownloadAsync(string url, string targetPath, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(targetPath)!;
        Directory.CreateDirectory(dir);
        var tmp = targetPath + ".tmp";
        long existing = File.Exists(tmp) ? new FileInfo(tmp).Length : 0;

        for (int attempt = 0; attempt < _settings.Current.RetryTimes; attempt++)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                if (existing > 0) req.Headers.TryAddWithoutValidation("Range", $"bytes={existing}-");
                req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36");
                req.Headers.TryAddWithoutValidation("Referer", "https://www.douyin.com/");

                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                if ((int)resp.StatusCode >= 400)
                {
                    Logger.Warn($"HTTP {(int)resp.StatusCode} 下载失败 {url}");
                    return new() { Success = false, Error = $"HTTP {(int)resp.StatusCode}" };
                }
                var contentLen = resp.Content.Headers.ContentLength ?? 0;
                var totalExpected = existing + contentLen;

                using (var fs = new FileStream(tmp, FileMode.Append, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                    var buf = new byte[81920];
                    int n;
                    while ((n = await stream.ReadAsync(buf, ct)) > 0)
                    {
                        await fs.WriteAsync(buf.AsMemory(0, n), ct);
                    }
                }

                if (contentLen > 0 && new FileInfo(tmp).Length != totalExpected)
                {
                    Logger.Warn($"完整性校验失败: 期望 {totalExpected}, 实际 {new FileInfo(tmp).Length}");
                    existing = 0;
                    File.Delete(tmp);
                    continue;
                }

                File.Move(tmp, targetPath, overwrite: true);
                return new() { Success = true, Bytes = new FileInfo(targetPath).Length };
            }
            catch (Exception ex)
            {
                Logger.Warn($"下载异常 ({attempt + 1}/{_settings.Current.RetryTimes}): {ex.Message}");
                if (attempt == _settings.Current.RetryTimes - 1)
                    return new() { Success = false, Error = ex.Message };
                existing = File.Exists(tmp) ? new FileInfo(tmp).Length : 0;
            }
        }
        return new() { Success = false, Error = "exhausted" };
    }

    private static HttpClient BuildDefaultHttp(string proxy)
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.None,
        };
        if (!string.IsNullOrEmpty(proxy))
        {
            handler.Proxy = new WebProxy(proxy);
            handler.UseProxy = true;
        }
        return new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(10) };
    }
}

public struct DownloadResult
{
    public bool Success;
    public long Bytes;
    public string? Error;
}

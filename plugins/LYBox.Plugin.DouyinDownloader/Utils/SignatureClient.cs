using System.Net.Http.Json;
using System.Text.Json;
using LYBox.Plugin.DouyinDownloader.Config;

namespace LYBox.Plugin.DouyinDownloader.Utils;

/// <summary>
/// 签名客户端：优先调用配置的第三方签名服务（X-Bogus / a_bogus），
/// 回退到本地 <see cref="XBogus"/> 1:1 实现。
/// </summary>
public sealed class SignatureClient
{
    private readonly HttpClient _http;
    private readonly DownloaderSettingsStore _settings;
    private readonly XBogus _localXbogus;

    public SignatureClient(DownloaderSettingsStore settings, HttpClient? http = null)
    {
        _settings = settings;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _localXbogus = new XBogus("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36");
    }

    public string UserAgent => _localXbogus.UserAgent;

    /// <summary>对 URL 追加 X-Bogus。第三方不可用时回退到本地。</summary>
    public async Task<(string SignedUrl, string XBogus, string UserAgent)> SignAsync(string url, CancellationToken ct = default)
    {
        var signerUrl = _settings.Current.SignerEndpoint?.Trim();
        if (!string.IsNullOrEmpty(signerUrl))
        {
            try
            {
                var req = new { url = url, ua = _localXbogus.UserAgent };
                using var resp = await _http.PostAsJsonAsync(signerUrl, req, ct);
                resp.EnsureSuccessStatusCode();
                var body = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                if (body.TryGetProperty("signed_url", out var su) && su.GetString() is { Length: > 0 } s
                    && body.TryGetProperty("user_agent", out var ua) && ua.GetString() is { Length: > 0 } u)
                {
                    var xb = body.TryGetProperty("x_bogus", out var x) ? x.GetString() ?? "" : "";
                    return (s, xb, u);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[SignatureClient] 第三方签名失败,回退到本地 XBogus: {ex.Message}");
            }
        }
        return _localXbogus.Build(url);
    }
}

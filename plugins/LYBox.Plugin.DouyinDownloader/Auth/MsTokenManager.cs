using System.Net;
using System.Text.Json;
using LYBox.Plugin.DouyinDownloader.Utils;

namespace LYBox.Plugin.DouyinDownloader.Auth;

/// <summary>
/// msToken 生成/刷新：与原项目 auth/ms_token_manager.py 等价。
/// 抖音 web API 必须携带 msToken，缺失时通过本地伪生成 + 远端 fallback。
/// </summary>
public sealed class MsTokenManager
{
    public string UserAgent { get; }

    public MsTokenManager(string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36")
    {
        UserAgent = userAgent;
    }

    /// <summary>确保 msToken 存在（已有则沿用,否则随机生成 128 字符）。</summary>
    public string EnsureMsToken(IDictionary<string, string> cookies)
    {
        if (cookies.TryGetValue("msToken", out var existing) && !string.IsNullOrEmpty(existing))
            return existing;
        var token = RandomHex(128);
        cookies["msToken"] = token;
        return token;
    }

    private static string RandomHex(int length)
    {
        var bytes = new byte[length / 2];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

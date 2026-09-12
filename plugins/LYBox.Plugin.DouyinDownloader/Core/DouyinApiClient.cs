using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using LYBox.Plugin.DouyinDownloader.Auth;
using LYBox.Plugin.DouyinDownloader.Config;
using LYBox.Plugin.DouyinDownloader.Models;
using LYBox.Plugin.DouyinDownloader.Utils;

namespace LYBox.Plugin.DouyinDownloader.Core;

/// <summary>
/// 抖音 Web API 异步客户端（1:1 移植 core/api_client.py 关键行为）：
/// - 默认 query 参数
/// - X-Bogus 签名
/// - 风险控制 (403/429) + 空 200 重试
/// - login_required 检测 (status_code==2483 / 请先登录)
/// - 短链 302 跟随
/// - 详情 / 用户作品 / 喜欢 / 合集 / 收藏 / 音乐 / 评论 / 热搜 / 搜索
/// </summary>
public sealed class DouyinApiClient
{
    public const string BaseUrl = "https://www.douyin.com";
    public const string LiveWebBase = "https://live.douyin.com";
    public const string LiveReflowBase = "https://webcast.amemv.com";

    private static readonly string[] DefaultUAs =
    {
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36",
    };

    private static readonly HashSet<int> RiskControlStatuses = new() { 403, 429 };
    private static readonly HashSet<int> LoginRequiredStatuses = new() { 2483 };

    /// <summary>写入浏览器 Cookie 时需屏蔽的会话键 (与原项目 _BROWSER_COOKIE_BLOCKLIST 一致)。</summary>
    public static readonly HashSet<string> BrowserCookieBlocklist = new(StringComparer.OrdinalIgnoreCase)
    {
        "sessionid","sessionid_ss","sid_tt","sid_guard",
        "uid_tt","uid_tt_ss","passport_auth_status","passport_auth_status_ss",
        "passport_assist_user","passport_auth_mix_state","passport_mfa_token","login_time",
    };

    private readonly CookieManager _cookies;
    private readonly SignatureClient _signer;
    private readonly DownloaderSettingsStore _settings;
    private readonly HttpClient _http;
    private readonly string _ua;
    private string _msToken;

    public DouyinApiClient(CookieManager cookies, SignatureClient signer, DownloaderSettingsStore settings, HttpClient? http = null)
    {
        _cookies = cookies;
        _signer = signer;
        _settings = settings;
        _http = http ?? new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            AllowAutoRedirect = false,
            UseCookies = false,
        })
        { Timeout = TimeSpan.FromSeconds(30) };
        _ua = DefaultUAs[Random.Shared.Next(DefaultUAs.Length)];
        _msToken = cookies.Cookies.TryGetValue("msToken", out var t) ? t : "";
    }

    // ================ 默认 query ================

    public async Task<Dictionary<string, string>> DefaultQueryAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_msToken))
        {
            var mgr = new MsTokenManager(_ua);
            _msToken = mgr.EnsureMsToken(_cookies.Cookies.ToDictionary(kv => kv.Key, kv => kv.Value));
        }

        return new(StringComparer.Ordinal)
        {
            ["device_platform"] = "webapp",
            ["aid"] = "6383",
            ["channel"] = "channel_pc_web",
            ["update_version_code"] = "170400",
            ["pc_client_type"] = "1",
            ["pc_libra_divert"] = "Windows",
            ["version_code"] = "290100",
            ["version_name"] = "29.1.0",
            ["cookie_enabled"] = "true",
            ["screen_width"] = "1536",
            ["screen_height"] = "864",
            ["browser_language"] = "zh-CN",
            ["browser_platform"] = "Win32",
            ["browser_name"] = "Chrome",
            ["browser_version"] = "139.0.0.0",
            ["browser_online"] = "true",
            ["engine_name"] = "Blink",
            ["engine_version"] = "139.0.0.0",
            ["os_name"] = "Windows",
            ["os_version"] = "10",
            ["cpu_core_num"] = "16",
            ["device_memory"] = "8",
            ["platform"] = "PC",
            ["downlink"] = "10",
            ["effective_type"] = "4g",
            ["round_trip_time"] = "200",
            ["support_h265"] = "1",
            ["support_dash"] = "1",
            ["uifid"] = "",
            ["msToken"] = _msToken,
        };
    }

    // ================ 通用 JSON 请求 ================

    public async Task<JsonElement> RequestJsonAsync(
        string path,
        Dictionary<string, string> query,
        HttpMethod method = null!,
        Dictionary<string, string>? data = null,
        Dictionary<string, string>? extraHeaders = null,
        string? baseUrl = null,
        int maxRetries = 3,
        CancellationToken ct = default)
    {
        method ??= HttpMethod.Get;
        baseUrl ??= BaseUrl;
        var delays = new[] { 1, 2, 5 };

        Exception? last = null;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var qs = string.Join("&", query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
                var fullUrl = $"{baseUrl.TrimEnd('/')}{path}?{qs}";
                var (signed, _, ua) = await _signer.SignAsync(fullUrl, ct);

                using var req = new HttpRequestMessage(method, signed);
                foreach (var kv in extraHeaders ?? new()) req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                req.Headers.TryAddWithoutValidation("User-Agent", ua);
                req.Headers.TryAddWithoutValidation("Referer", baseUrl.Contains("live") ? "https://live.douyin.com/" : "https://www.douyin.com/?recommend=1");
                req.Headers.TryAddWithoutValidation("Accept", "*/*");
                req.Headers.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9");
                req.Headers.TryAddWithoutValidation("Cookie", _cookies.CookieHeader());

                if (method == HttpMethod.Post && data != null)
                {
                    req.Content = new FormUrlEncodedContent(data);
                }

                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                if (resp.StatusCode == HttpStatusCode.OK)
                {
                    var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                    if (bytes.Length == 0)
                    {
                        Logger.Warn($"[{path}] 空 200 响应 (attempt {attempt + 1}/{maxRetries}),疑似反爬");
                        last = new InvalidOperationException("Empty 200 response");
                        await Delay(attempt, delays, ct); continue;
                    }
                    try
                    {
                        var doc = JsonDocument.Parse(bytes);
                        var root = doc.RootElement.Clone();
                        if (IsLoginRequired(root)) throw new LoginRequiredException(root);
                        return root;
                    }
                    catch (JsonException)
                    {
                        Logger.Warn($"[{path}] 非 JSON 响应,长度={bytes.Length}");
                        return default;
                    }
                }
                if (RiskControlStatuses.Contains((int)resp.StatusCode))
                {
                    Logger.Warn($"[{path}] 风控触发 {(int)resp.StatusCode} (attempt {attempt + 1}/{maxRetries})");
                    last = new HttpRequestException($"HTTP {(int)resp.StatusCode}");
                    await Delay(attempt, delays, ct); continue;
                }
                if ((int)resp.StatusCode < 500)
                {
                    Logger.Error($"[{path}] HTTP {(int)resp.StatusCode}");
                    return default;
                }
                last = new HttpRequestException($"HTTP {(int)resp.StatusCode}");
                await Delay(attempt, delays, ct);
            }
            catch (LoginRequiredException) { throw; }
            catch (Exception ex)
            {
                Logger.Warn($"[{path}] attempt {attempt + 1} 异常: {ex.Message}");
                last = ex;
                await Delay(attempt, delays, ct);
            }
        }
        Logger.Error($"[{path}] 重试 {maxRetries} 次后仍失败: {last?.Message}");
        return default;
    }

    private static async Task Delay(int attempt, int[] delays, CancellationToken ct)
    {
        var s = delays[Math.Min(attempt, delays.Length - 1)];
        await Task.Delay(TimeSpan.FromSeconds(s), ct);
    }

    private static bool IsLoginRequired(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return false;
        if (root.TryGetProperty("status_code", out var sc) && sc.ValueKind == JsonValueKind.Number
            && LoginRequiredStatuses.Contains(sc.GetInt32())) return true;
        if (root.TryGetProperty("status_msg", out var sm) && sm.ValueKind == JsonValueKind.String)
        {
            var msg = sm.GetString() ?? "";
            if (msg.Contains("请先登录") || msg.Contains("用户未登录")) return true;
        }
        return false;
    }

    // ================ 业务端点 ================

    public async Task<AwemeDetail?> GetVideoDetailAsync(string awemeId, CancellationToken ct = default)
    {
        foreach (var aid in new[] { "6383", "1128" })
        {
            var q = await DefaultQueryAsync(ct);
            q["aweme_id"] = awemeId;
            q["aid"] = aid;
            var data = await RequestJsonAsync("/aweme/v1/web/aweme/detail/", q, maxRetries: 3, ct: ct);
            if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("aweme_detail", out var det)
                && det.ValueKind == JsonValueKind.Object)
                return ParseAweme(det);
        }
        return null;
    }

    public async Task<PagedResult<AwemeListItem>> GetUserPostAsync(string secUid, long maxCursor = 0, int count = 18, CancellationToken ct = default)
    {
        var q = await DefaultQueryAsync(ct);
        q["sec_user_id"] = secUid;
        q["max_cursor"] = maxCursor.ToString();
        q["count"] = count.ToString();
        q["locate_query"] = "false";
        q["show_live_replay_strategy"] = "1";
        q["need_time_list"] = "1";
        q["time_list_query"] = "0";
        q["whale_cut_token"] = "";
        q["cut_version"] = "1";
        q["publish_video_strategy_type"] = "2";
        var data = await RequestJsonAsync("/aweme/v1/web/aweme/post/", q, ct: ct);
        return ParsePaged(data, "aweme_list", ParseAwemeListItem);
    }

    public async Task<PagedResult<AwemeListItem>> GetUserLikeAsync(string secUid, long maxCursor = 0, int count = 20, CancellationToken ct = default)
    {
        var q = await DefaultQueryAsync(ct);
        q["sec_user_id"] = secUid;
        q["max_cursor"] = maxCursor.ToString();
        q["count"] = count.ToString();
        q["locate_query"] = "false";
        var data = await RequestJsonAsync("/aweme/v1/web/aweme/favorite/", q, ct: ct);
        return ParsePaged(data, "aweme_list", ParseAwemeListItem);
    }

    public async Task<PagedResult<MixInfo>> GetUserMixAsync(string secUid, long maxCursor = 0, int count = 20, CancellationToken ct = default)
    {
        var q = await DefaultQueryAsync(ct);
        q["sec_user_id"] = secUid;
        q["max_cursor"] = maxCursor.ToString();
        q["count"] = count.ToString();
        q["locate_query"] = "false";
        var data = await RequestJsonAsync("/aweme/v1/web/mix/list/", q, ct: ct);
        return ParsePaged(data, "mix_infos", ParseMixInfo);
    }

    public async Task<PagedResult<AwemeListItem>> GetMixAwemeAsync(string mixId, long cursor = 0, int count = 20, CancellationToken ct = default)
    {
        var q = await DefaultQueryAsync(ct);
        q["mix_id"] = mixId;
        q["cursor"] = cursor.ToString();
        q["count"] = count.ToString();
        var data = await RequestJsonAsync("/aweme/v1/web/mix/aweme/", q, ct: ct);
        return ParsePaged(data, "aweme_list", ParseAwemeListItem);
    }

    public async Task<PagedResult<AwemeListItem>> GetUserCollectionAsync(string secUid = "self", long maxCursor = 0, int count = 20, CancellationToken ct = default)
    {
        if (secUid != "self") return new();
        var q = await DefaultQueryAsync(ct);
        q["publish_video_strategy_type"] = "2";
        q["version_code"] = "170400";
        q["version_name"] = "17.4.0";
        var data = await RequestJsonAsync("/aweme/v1/web/aweme/listcollection/", q,
            method: HttpMethod.Post,
            data: new() { ["count"] = count.ToString(), ["cursor"] = maxCursor.ToString() },
            extraHeaders: new()
            {
                ["Content-Type"] = "application/x-www-form-urlencoded",
                ["Referer"] = "https://www.douyin.com/user/self?showTab=favorite_collection",
            },
            ct: ct);
        return ParsePaged(data, "aweme_list", ParseAwemeListItem);
    }

    public async Task<PagedResult<MixInfo>> GetUserCollectMixAsync(string secUid = "self", long maxCursor = 0, int count = 12, CancellationToken ct = default)
    {
        if (secUid != "self") return new();
        var q = await DefaultQueryAsync(ct);
        q["cursor"] = maxCursor.ToString();
        q["count"] = count.ToString();
        q["version_code"] = "170400";
        q["version_name"] = "17.4.0";
        var data = await RequestJsonAsync("/aweme/v1/web/mix/listcollection/", q, ct: ct);
        return ParsePaged(data, "mix_infos", ParseMixInfo);
    }

    public async Task<PagedResult<MusicInfo>> GetMusicAwemeAsync(string musicId, long cursor = 0, int count = 20, CancellationToken ct = default)
    {
        var q = await DefaultQueryAsync(ct);
        q["music_id"] = musicId;
        q["cursor"] = cursor.ToString();
        q["count"] = count.ToString();
        var data = await RequestJsonAsync("/aweme/v1/web/music/aweme/", q, ct: ct);
        var page = ParsePaged(data, "aweme_list", ParseAwemeListItem);
        return new PagedResult<MusicInfo> { HasMore = page.HasMore, MaxCursor = page.MaxCursor, StatusCode = page.StatusCode };
    }

    public async Task<JsonElement> SearchAsync(string keyword, int offset = 0, int count = 10, int sortType = 0, int publishTime = 0, CancellationToken ct = default)
    {
        var q = await DefaultQueryAsync(ct);
        q["keyword"] = keyword;
        q["search_channel"] = "aweme_video_web";
        q["sort_type"] = sortType.ToString();
        q["publish_time"] = publishTime.ToString();
        q["search_source"] = "normal_search";
        q["query_correct_type"] = "1";
        q["is_filter_search"] = (sortType != 0 || publishTime != 0) ? "1" : "0";
        q["offset"] = offset.ToString();
        q["count"] = count.ToString();
        return await RequestJsonAsync("/aweme/v1/web/general/search/single/", q, maxRetries: 1, ct: ct);
    }

    public async Task<JsonElement> GetHotBoardAsync(CancellationToken ct = default)
    {
        var q = await DefaultQueryAsync(ct);
        q["detail_list"] = "1";
        q["source"] = "6";
        return await RequestJsonAsync("/aweme/v1/web/hot/search/list/", q, maxRetries: 1, ct: ct);
    }

    // ================ 直播 ================

    public sealed class LiveRoomInfo
    {
        public string RoomId { get; set; } = "";
        public string Title { get; set; } = "";
        public string AuthorName { get; set; } = "";
        public string AuthorSecUid { get; set; } = "";
        public string StreamUrl { get; set; } = "";
        public string Quality { get; set; } = "";
        public bool IsHls { get; set; }
        public int Status { get; set; }       // 2 = live, 4 = offline
        public JsonElement Raw { get; set; }
    }

    public sealed class LiveReplayInfo
    {
        public string EpisodeId { get; set; } = "";
        public string RoomId { get; set; } = "";
        public string Title { get; set; } = "";
        public string AuthorName { get; set; } = "";
        public string VideoUrl { get; set; } = "";
        public string AudioUrl { get; set; } = "";
        public JsonElement Raw { get; set; }
    }

    public async Task<LiveRoomInfo?> GetLiveRoomInfoAsync(string roomId, string? secUserId = null, string roomIdKind = "web_rid", CancellationToken ct = default)
    {
        string path; Dictionary<string, string> q; string baseUrl;
        q = await DefaultQueryAsync(ct);
        if (roomIdKind == "room_id")
        {
            q["room_id"] = roomId;
            q["sec_user_id"] = secUserId ?? "";
            q["type_id"] = "0";
            q["live_id"] = "1";
            q["app_id"] = "1128";
            q["version_code"] = "99.99.99";
            path = "/webcast/room/reflow/info/";
            baseUrl = LiveReflowBase;
        }
        else
        {
            q["web_rid"] = roomId;
            q["app_name"] = "douyin_web";
            q["live_id"] = "1";
            q["device_platform"] = "web";
            q["language"] = "zh-CN";
            q["enter_source"] = "";
            q["is_need_double_stream"] = "false";
            q["cookie_enabled"] = "true";
            path = "/webcast/room/web/enter/";
            baseUrl = LiveWebBase;
        }
        var resp = await RequestJsonAsync(path, q, maxRetries: 1, baseUrl: baseUrl,
            extraHeaders: new Dictionary<string, string>
            {
                ["Referer"] = "https://live.douyin.com/",
                ["Origin"] = "https://live.douyin.com",
            }, ct: ct);
        var info = ParseLiveRoom(resp);
        if (info != null || roomIdKind == "room_id") return info;
        // web_rid 失败 → SSR fallback
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"{LiveWebBase}/{roomId}");
            req.Headers.TryAddWithoutValidation("User-Agent", _ua);
            req.Headers.TryAddWithoutValidation("Referer", "https://live.douyin.com/");
            using var httpResp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (httpResp.IsSuccessStatusCode)
            {
                var html = await httpResp.Content.ReadAsStringAsync(ct);
                var ssr = ExtractLiveRoomFromHtml(html);
                if (ssr != null) return ssr;
            }
        }
        catch { }
        return null;
    }

    public async Task<LiveReplayInfo?> GetLiveReplayInfoAsync(string episodeId, string? replayId = null, CancellationToken ct = default)
    {
        var q = await DefaultQueryAsync(ct);
        q["channel"] = "";
        q["episode_id"] = episodeId;
        var resp1 = await RequestJsonAsync("/aweme/v1/web/show/episode/enter/", q, maxRetries: 1, ct: ct);
        string? roomId = null;
        if (resp1.ValueKind == JsonValueKind.Object && resp1.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "episode", "episode_info", "show_episode" })
                if (d.TryGetProperty(key, out var ep) && ep.ValueKind == JsonValueKind.Object)
                { roomId ??= ep.TryGetProperty("room_id", out var rid) && rid.ValueKind == JsonValueKind.Number ? rid.GetInt64().ToString() : null; break; }
        }
        if (string.IsNullOrEmpty(roomId)) return null;

        var q2 = await DefaultQueryAsync(ct);
        q2["channel"] = "";
        q2["episode_id"] = episodeId;
        q2["room_id"] = roomId;
        if (!string.IsNullOrEmpty(replayId)) q2["replay_id"] = replayId;
        var resp2 = await RequestJsonAsync("/aweme/v1/web/show/episode/replay_list/", q2, maxRetries: 1, ct: ct);
        var info = ParseLiveReplay(resp2, episodeId, replayId);
        if (info == null) return null;
        info.RoomId = roomId;
        return info;
    }

    private static LiveRoomInfo? ParseLiveRoom(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        var data = root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object ? d : root;
        JsonElement? room = null;
        if (data.TryGetProperty("room", out var r) && r.ValueKind == JsonValueKind.Object) room = r;
        else if (data.TryGetProperty("data", out var list) && list.ValueKind == JsonValueKind.Array && list.GetArrayLength() > 0 && list[0].ValueKind == JsonValueKind.Object) room = list[0];
        else if (root.TryGetProperty("room", out var rr) && rr.ValueKind == JsonValueKind.Object) room = rr;
        if (room == null) return null;
        var rr2 = room.Value;
        var info = new LiveRoomInfo { Raw = root };
        info.RoomId = rr2.TryGetProperty("id", out var rid) && rid.ValueKind == JsonValueKind.Number ? rid.GetInt64().ToString() : "";
        info.Title = rr2.TryGetProperty("title", out var t) ? t.GetString() ?? "直播" : "直播";
        info.Status = rr2.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.Number ? st.GetInt32() : 4;
        if (data.TryGetProperty("user", out var u) && u.ValueKind == JsonValueKind.Object)
        {
            info.AuthorName = u.TryGetProperty("nickname", out var n) ? n.GetString() ?? "" : "";
            info.AuthorSecUid = u.TryGetProperty("sec_uid", out var s) ? s.GetString() ?? "" : "";
        }
        else if (rr2.TryGetProperty("owner", out var own) && own.ValueKind == JsonValueKind.Object)
        {
            info.AuthorName = own.TryGetProperty("nickname", out var n) ? n.GetString() ?? "" : "";
        }
        if (rr2.TryGetProperty("stream_url", out var su) && su.ValueKind == JsonValueKind.Object)
        {
            string? bestUrl = null; string bestQuality = "";
            // FLV first
            if (su.TryGetProperty("flv_pull_url", out var flv) && flv.ValueKind == JsonValueKind.Object)
                foreach (var prop in flv.EnumerateObject())
                    if (prop.Value.ValueKind == JsonValueKind.String) { bestUrl = prop.Value.GetString(); bestQuality = prop.Name; break; }
            if (bestUrl == null && su.TryGetProperty("hls_pull_url_map", out var hls) && hls.ValueKind == JsonValueKind.Object)
                foreach (var prop in hls.EnumerateObject())
                    if (prop.Value.ValueKind == JsonValueKind.String) { bestUrl = prop.Value.GetString(); bestQuality = prop.Name; break; }
            info.StreamUrl = bestUrl ?? "";
            info.Quality = bestQuality;
            info.IsHls = info.StreamUrl.Contains(".m3u8");
        }
        return info;
    }

    private static LiveReplayInfo? ParseLiveReplay(JsonElement root, string episodeId, string? replayId)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        JsonElement? data = root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object ? d : null;
        if (data == null) return null;

        JsonElement? candidate = null;
        foreach (var key in new[] { "replay", "replay_info", "current_replay" })
            if (data.Value.TryGetProperty(key, out var x) && x.ValueKind == JsonValueKind.Object) { candidate = x; break; }
        if (candidate == null)
        {
            foreach (var key in new[] { "info_list", "replay_list", "replays" })
                if (data.Value.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
                { candidate = arr[0]; break; }
        }
        if (candidate == null) return null;

        var info = new LiveReplayInfo { EpisodeId = episodeId, Raw = root };
        var c = candidate.Value;
        if (c.TryGetProperty("title", out var ti)) info.Title = ti.GetString() ?? "直播回放";
        else if (c.TryGetProperty("replay_title", out var rt)) info.Title = rt.GetString() ?? "直播回放";

        // 视频 / 音轨 URL 提取
        if (c.TryGetProperty("video_info", out var vi) && vi.ValueKind == JsonValueKind.Object)
        {
            if (vi.TryGetProperty("play_url", out var pu) && pu.ValueKind == JsonValueKind.Object
                && pu.TryGetProperty("url_list", out var ul) && ul.ValueKind == JsonValueKind.Array && ul.GetArrayLength() > 0)
                info.VideoUrl = ul[0].GetString() ?? "";
        }
        if (c.TryGetProperty("audio_info", out var ai) && ai.ValueKind == JsonValueKind.Object)
        {
            if (ai.TryGetProperty("play_url", out var pu) && pu.ValueKind == JsonValueKind.Object
                && pu.TryGetProperty("url_list", out var ul) && ul.ValueKind == JsonValueKind.Array && ul.GetArrayLength() > 0)
                info.AudioUrl = ul[0].GetString() ?? "";
        }
        return info;
    }

    private static LiveRoomInfo? ExtractLiveRoomFromHtml(string html)
    {
        // 简化实现: 仅识别页面 JSON 中的 room 信息。
        try
        {
            var marker = "\"room\":{";
            var idx = html.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) return null;
            var window = html.Substring(idx, Math.Min(html.Length - idx, 200_000));
            using var doc = JsonDocument.Parse("{" + window.Substring(0, window.IndexOf("}", StringComparison.Ordinal) + 1) + "}");
            return ParseLiveRoom(doc.RootElement);
        }
        catch { return null; }
    }

    public async Task<string?> ResolveShortUrlAsync(string shortUrl, CancellationToken ct = default)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, UrlParser.NormalizeShortUrl(shortUrl));
            req.Headers.TryAddWithoutValidation("User-Agent", _ua);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            var final = resp.Headers.Location != null ? (resp.Headers.Location.IsAbsoluteUri ? resp.Headers.Location.AbsoluteUri : new Uri(new Uri(BaseUrl), resp.Headers.Location).AbsoluteUri) : shortUrl;
            if ((int)resp.StatusCode >= 400) return null;
            return final;
        }
        catch (Exception ex)
        {
            Logger.Warn($"短链解析失败: {ex.Message}");
            return null;
        }
    }

    // ================ 解析辅助 ================

    private static PagedResult<T> ParsePaged<T>(JsonElement data, string key, Func<JsonElement, T> map)
    {
        var page = new PagedResult<T>();
        if (data.ValueKind != JsonValueKind.Object) return page;
        if (data.TryGetProperty(key, out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in list.EnumerateArray())
                page.Items.Add(map(item));
        }
        if (data.TryGetProperty("has_more", out var hm))
        {
            page.HasMore = hm.ValueKind == JsonValueKind.Number ? hm.GetInt32() != 0 : (hm.ValueKind == JsonValueKind.True || (hm.ValueKind == JsonValueKind.String && bool.TryParse(hm.GetString(), out var b) && b));
        }
        if (data.TryGetProperty("max_cursor", out var mc) && mc.ValueKind == JsonValueKind.Number) page.MaxCursor = mc.GetInt64();
        if (data.TryGetProperty("status_code", out var sc) && sc.ValueKind == JsonValueKind.Number) page.StatusCode = sc.GetInt32();
        return page;
    }

    private static AwemeListItem ParseAwemeListItem(JsonElement e)
    {
        var item = new AwemeListItem();
        if (e.TryGetProperty("aweme_id", out var id)) item.AwemeId = id.ToString();
        if (e.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String) item.Title = t.GetString() ?? "";
        else if (e.TryGetProperty("desc", out var d)) item.Title = d.GetString() ?? "";
        if (e.TryGetProperty("create_time", out var ct) && ct.ValueKind == JsonValueKind.Number) item.CreateTime = ct.GetInt64();
        if (e.TryGetProperty("author", out var a) && a.ValueKind == JsonValueKind.Object)
        {
            if (a.TryGetProperty("nickname", out var n)) item.AuthorName = n.GetString() ?? "";
            if (a.TryGetProperty("sec_uid", out var s)) item.AuthorSecUid = s.GetString() ?? "";
        }
        return item;
    }

    public sealed class MixInfo { public string MixId { get; set; } = ""; public string Title { get; set; } = ""; public string Cover { get; set; } = ""; public int VideoCount { get; set; } }

    private static MixInfo ParseMixInfo(JsonElement e)
    {
        var m = new MixInfo();
        if (e.TryGetProperty("mix_id", out var id)) m.MixId = id.ToString();
        if (e.TryGetProperty("mix_name", out var n)) m.Title = n.GetString() ?? "";
        if (e.TryGetProperty("cover_url", out var c) && c.ValueKind == JsonValueKind.Object && c.TryGetProperty("url_list", out var u) && u.ValueKind == JsonValueKind.Array && u.GetArrayLength() > 0) m.Cover = u[0].GetString() ?? "";
        if (e.TryGetProperty("video_count", out var vc) && vc.ValueKind == JsonValueKind.Number) m.VideoCount = vc.GetInt32();
        return m;
    }

    public sealed class MusicInfo { public string MusicId { get; set; } = ""; public string Title { get; set; } = ""; public string Author { get; set; } = ""; public string PlayUrl { get; set; } = ""; public string Cover { get; set; } = ""; }

    private static MusicInfo ParseMusic(JsonElement e)
    {
        var m = new MusicInfo();
        if (e.TryGetProperty("id", out var id) || e.TryGetProperty("music_id", out id)) m.MusicId = id.ToString();
        if (e.TryGetProperty("title", out var t)) m.Title = t.GetString() ?? "";
        if (e.TryGetProperty("author", out var a)) m.Author = a.GetString() ?? "";
        if (e.TryGetProperty("play_url", out var p) && p.ValueKind == JsonValueKind.Object && p.TryGetProperty("url_list", out var ul) && ul.ValueKind == JsonValueKind.Array && ul.GetArrayLength() > 0) m.PlayUrl = ul[0].GetString() ?? "";
        if (e.TryGetProperty("cover", out var c) && c.ValueKind == JsonValueKind.Object && c.TryGetProperty("url_list", out var cul) && cul.ValueKind == JsonValueKind.Array && cul.GetArrayLength() > 0) m.Cover = cul[0].GetString() ?? "";
        return m;
    }

    private static AwemeDetail ParseAweme(JsonElement e)
    {
        var a = new AwemeDetail();
        if (e.TryGetProperty("aweme_id", out var id)) a.AwemeId = id.ToString();
        if (e.TryGetProperty("desc", out var d)) { a.Desc = d.GetString() ?? ""; a.Title = a.Desc; }
        if (e.TryGetProperty("create_time", out var ct) && ct.ValueKind == JsonValueKind.Number) a.CreateTime = ct.GetInt64();
        if (e.TryGetProperty("aweme_type", out var at)) a.AwemeType = at.ToString();
        if (e.TryGetProperty("author", out var auth) && auth.ValueKind == JsonValueKind.Object)
        {
            if (auth.TryGetProperty("nickname", out var n)) a.AuthorName = n.GetString() ?? "";
            if (auth.TryGetProperty("uid", out var uid)) a.AuthorId = uid.ToString();
            if (auth.TryGetProperty("sec_uid", out var suid)) a.AuthorSecUid = suid.ToString() ?? "";
            if (auth.TryGetProperty("avatar_thumb", out var av) && av.ValueKind == JsonValueKind.Object && av.TryGetProperty("url_list", out var aul) && aul.ValueKind == JsonValueKind.Array && aul.GetArrayLength() > 0)
                a.AuthorAvatar = aul[0].GetString() ?? "";
        }
        if (e.TryGetProperty("video", out var v) && v.ValueKind == JsonValueKind.Object)
        {
            ExtractUrlsFromCoverLike(v, "cover", a.CoverUrls);
            if (v.TryGetProperty("play_addr", out var pa) && pa.ValueKind == JsonValueKind.Object
                && pa.TryGetProperty("url_list", out var ul) && ul.ValueKind == JsonValueKind.Array)
                foreach (var s in ul.EnumerateArray()) if (s.ValueKind == JsonValueKind.String) a.VideoUrls.Add(s.GetString()!);
            if (v.TryGetProperty("bit_rate", out var br) && br.ValueKind == JsonValueKind.Array)
            {
                long best = -1; string? bestUrl = null;
                foreach (var brItem in br.EnumerateArray())
                {
                    if (brItem.ValueKind != JsonValueKind.Object) continue;
                    if (brItem.TryGetProperty("bit_rate", out var r) && r.ValueKind == JsonValueKind.Number)
                    {
                        var rate = r.GetInt64();
                        if (rate <= best) continue;
                        var noWm = false;
                        if (brItem.TryGetProperty("play_addr", out var pa2) && pa2.ValueKind == JsonValueKind.Object
                            && pa2.TryGetProperty("url_list", out var ul2) && ul2.ValueKind == JsonValueKind.Array && ul2.GetArrayLength() > 0)
                        {
                            var url = ul2[0].GetString();
                            if (url != null && url.Contains("playwm")) noWm = true;
                            best = rate; bestUrl = url;
                        }
                    }
                }
                a.BitRate = best;
                a.BestVideoUrl = bestUrl ?? "";
            }
        }
        if (e.TryGetProperty("images", out var imgs) && imgs.ValueKind == JsonValueKind.Array)
        {
            foreach (var img in imgs.EnumerateArray())
            {
                var asset = new ImageAsset();
                if (img.TryGetProperty("origin_image", out var oi) && oi.ValueKind == JsonValueKind.Object)
                    FillUrls(oi, asset.NoWatermarkUrls);
                if (img.TryGetProperty("display_image", out var di) && di.ValueKind == JsonValueKind.Object)
                    FillUrls(di, asset.NoWatermarkUrls);
                if (img.TryGetProperty("download_url_list", out var du) && du.ValueKind == JsonValueKind.Array)
                    foreach (var s in du.EnumerateArray()) if (s.ValueKind == JsonValueKind.String) asset.WatermarkUrls.Add(s.GetString()!);
                if (img.TryGetProperty("owner_watermark_image", out var ow) && ow.ValueKind == JsonValueKind.Object)
                    FillUrls(ow, asset.WatermarkUrls);
                a.Images.Add(asset);
            }
        }
        if (e.TryGetProperty("music", out var m) && m.ValueKind == JsonValueKind.Object)
        {
            var ms = new MusicAsset();
            if (m.TryGetProperty("id", out var mid)) ms.Id = mid.ToString();
            if (m.TryGetProperty("title", out var mt)) ms.Title = mt.GetString() ?? "";
            if (m.TryGetProperty("author", out var ma)) ms.Author = ma.GetString() ?? "";
            if (m.TryGetProperty("play_url", out var pu) && pu.ValueKind == JsonValueKind.Object
                && pu.TryGetProperty("url_list", out var pul) && pul.ValueKind == JsonValueKind.Array && pul.GetArrayLength() > 0)
                ms.PlayUrl = pul[0].GetString() ?? "";
            if (m.TryGetProperty("cover", out var mc) && mc.ValueKind == JsonValueKind.Object
                && mc.TryGetProperty("url_list", out var mcl) && mcl.ValueKind == JsonValueKind.Array && mcl.GetArrayLength() > 0)
                ms.CoverUrl = mcl[0].GetString() ?? "";
            a.Music = ms;
        }
        // 无水印优先: 抖音通常在 play_addr 第二条之后是无水印
        if (a.VideoUrls.Count > 1)
        {
            var noWm = a.VideoUrls.FirstOrDefault(u => !u.Contains("playwm"));
            a.BestNoWatermarkUrl = noWm ?? "";
            if (string.IsNullOrEmpty(a.BestVideoUrl)) a.BestVideoUrl = a.VideoUrls[0];
        }
        return a;
    }

    private static void FillUrls(JsonElement e, List<string> list)
    {
        if (e.TryGetProperty("url_list", out var ul) && ul.ValueKind == JsonValueKind.Array)
            foreach (var s in ul.EnumerateArray()) if (s.ValueKind == JsonValueKind.String) list.Add(s.GetString()!);
    }

    private static void ExtractUrlsFromCoverLike(JsonElement parent, string key, List<string> list)
    {
        if (parent.TryGetProperty(key, out var c) && c.ValueKind == JsonValueKind.Object)
            FillUrls(c, list);
    }

}

public sealed class LoginRequiredException : Exception
{
    public LoginRequiredException(JsonElement root) : base(Format(root))
    {
    }
    private static string Format(JsonElement r)
    {
        var msg = r.TryGetProperty("status_msg", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : "(empty)";
        var code = r.TryGetProperty("status_code", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : 0;
        return $"login required (status_code={code}) {msg}";
    }
}

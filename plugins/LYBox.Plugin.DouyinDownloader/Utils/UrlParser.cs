using System.Text.RegularExpressions;
using LYBox.Plugin.DouyinDownloader.Models;

namespace LYBox.Plugin.DouyinDownloader.Utils;

/// <summary>
/// 抖音 URL 分类与 ID 提取（与原项目 utils/validators.py + core/url_parser.py 行为对齐）。
/// </summary>
public static class UrlParser
{
    private static readonly string[] ShortHosts =
    {
        "v.douyin.com",
        "v.iesdouyin.com",
        "iesdouyin.com",
    };

    private static readonly HashSet<string> WindowsReserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON","PRN","AUX","NUL",
        "COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
        "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9",
    };

    public static bool IsShortUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        var s = url.Trim();
        foreach (var scheme in new[] { "https://", "http://" })
        {
            if (s.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
            {
                s = s[scheme.Length..];
                break;
            }
        }
        return ShortHosts.Any(h => s.StartsWith(h + "/", StringComparison.OrdinalIgnoreCase) || string.Equals(s, h, StringComparison.OrdinalIgnoreCase));
    }

    public static string NormalizeShortUrl(string url)
    {
        var s = (url ?? "").Trim();
        if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return s;
        return "https://" + s;
    }

    /// <summary>解析 URL 类型与各字段。返回 null 表示不支持。</summary>
    public static ParsedUrl? Parse(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        if (IsShortUrl(url))
        {
            return new ParsedUrl { Kind = UrlKind.Short, OriginalUrl = url };
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var host = (uri.Host ?? "").ToLowerInvariant();
        var path = uri.AbsolutePath ?? "";
        var query = ParseQuery(uri.Query ?? "");

        // modal_id → 视频弹窗
        if (query.TryGetValue("modal_id", out var modalIds) && modalIds.Count > 0 && !string.IsNullOrWhiteSpace(modalIds[0]))
        {
            return new ParsedUrl
            {
                Kind = UrlKind.Video,
                OriginalUrl = url,
                AwemeId = modalIds[0].Trim(),
            };
        }

        // 直播回放 / webcast reflow
        if (host == "webcast.amemv.com" && Regex.IsMatch(path, @"^/douyin/webcast/reflow/episode/\d+/?$"))
        {
            var (episodeId, replayId) = ExtractLiveReplayIds(uri);
            return new ParsedUrl
            {
                Kind = UrlKind.LiveReplay,
                OriginalUrl = url,
                EpisodeId = episodeId,
                ReplayId = replayId,
            };
        }
        if (host == "webcast.amemv.com" && Regex.IsMatch(path, @"^/douyin/webcast/reflow/\d+/?$"))
        {
            var room = ExtractLiveRoomIds(uri);
            return new ParsedUrl
            {
                Kind = UrlKind.Live,
                OriginalUrl = url,
                RoomId = room.RoomId,
                RoomIdKind = room.Kind,
                SecUserId = room.SecUserId,
            };
        }
        if (host == "douyin.com" || host.EndsWith(".douyin.com") || host == "iesdouyin.com" || host.EndsWith(".iesdouyin.com"))
        {
            if (Regex.IsMatch(path, @"^/vsdetail/\d+/?$"))
            {
                var (episodeId, replayId) = ExtractLiveReplayIds(uri);
                return new ParsedUrl { Kind = UrlKind.LiveReplay, OriginalUrl = url, EpisodeId = episodeId, ReplayId = replayId };
            }
        }
        if (host == "live.douyin.com" && Regex.IsMatch(path, @"^/\d+/?$"))
        {
            return new ParsedUrl
            {
                Kind = UrlKind.Live,
                OriginalUrl = url,
                RoomId = Regex.Match(path, @"\d+").Value,
                RoomIdKind = "web_rid",
            };
        }
        if (host == "www.douyin.com" || host == "douyin.com" || host.EndsWith(".douyin.com"))
        {
            if (Regex.IsMatch(path, @"^/(?:follow/|share/)?live/\d+/?$"))
            {
                return new ParsedUrl
                {
                    Kind = UrlKind.Live,
                    OriginalUrl = url,
                    RoomId = Regex.Match(path, @"\d+").Value,
                    RoomIdKind = "web_rid",
                };
            }
        }
        if (host == "douyin.com" || host.EndsWith(".douyin.com"))
        {
            if (path.Contains("/lvdetail/")) return new ParsedUrl { Kind = UrlKind.Unknown, OriginalUrl = url }; // 拒绝：影院版权
            var videoId = ExtractVideoId(path);
            if (videoId != null) return new ParsedUrl { Kind = UrlKind.Video, OriginalUrl = url, AwemeId = videoId };
            if (path.Contains("/user/"))
            {
                var secUid = ExtractUserId(path);
                if (secUid != null) return new ParsedUrl { Kind = UrlKind.User, OriginalUrl = url, SecUid = secUid };
            }
            if (path.Contains("/note/") || path.Contains("/gallery/") || path.Contains("/slides/"))
            {
                var noteId = ExtractNoteId(path);
                if (noteId != null)
                    return new ParsedUrl { Kind = UrlKind.Note, OriginalUrl = url, NoteId = noteId, AwemeId = noteId };
            }
            if (path.Contains("/collection/") || path.Contains("/mix/"))
            {
                var mixId = ExtractMixId(path);
                if (mixId != null) return new ParsedUrl { Kind = UrlKind.Collection, OriginalUrl = url, MixId = mixId };
            }
            if (path.Contains("/music/"))
            {
                var musicId = ExtractMusicId(path);
                if (musicId != null) return new ParsedUrl { Kind = UrlKind.Music, OriginalUrl = url, MusicId = musicId };
            }
        }
        return null;
    }

    private static string? ExtractVideoId(string path)
    {
        var m = Regex.Match(path, @"/video/(\d+)");
        return m.Success ? m.Groups[1].Value : null;
    }
    private static string? ExtractUserId(string path)
    {
        var m = Regex.Match(path, @"/user/([A-Za-z0-9_\-]+)");
        return m.Success ? m.Groups[1].Value : null;
    }
    private static string? ExtractMixId(string path)
    {
        var m = Regex.Match(path, @"/(?:collection|mix)/(\d+)");
        return m.Success ? m.Groups[1].Value : null;
    }
    private static string? ExtractNoteId(string path)
    {
        var m = Regex.Match(path, @"/(?:note|gallery|slides)/(\d+)");
        return m.Success ? m.Groups[1].Value : null;
    }
    private static string? ExtractMusicId(string path)
    {
        var m = Regex.Match(path, @"/music/(\d+)");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static (string? EpisodeId, string? ReplayId) ExtractLiveReplayIds(Uri uri)
    {
        var path = uri.AbsolutePath;
        string? episodeId = null;
        var m = Regex.Match(path, @"^/vsdetail/(\d+)(?:/|$)");
        if (!m.Success)
            m = Regex.Match(path, @"^/douyin/webcast/reflow/episode/(\d+)(?:/|$)");
        if (m.Success) episodeId = m.Groups[1].Value;
        var query = ParseQuery(uri.Query);
        string? replayId = null;
        if (query.TryGetValue("replay_id", out var rs) && rs.Count > 0 && rs[0].All(char.IsDigit))
            replayId = rs[0];
        return (episodeId, replayId);
    }

    private record LiveRoomRef(string? RoomId, string Kind, string? SecUserId);

    private static LiveRoomRef ExtractLiveRoomIds(Uri uri)
    {
        var m = Regex.Match(uri.AbsolutePath, @"^/douyin/webcast/reflow/(\d+)/?$");
        if (!m.Success) return new(null, "web_rid", null);
        var sec = ParseQuery(uri.Query).GetValueOrDefault("sec_user_id");
        return new(m.Groups[1].Value, "room_id", sec is { Count: > 0 } ? sec[0] : null);
    }

    private static Dictionary<string, List<string>> ParseQuery(string raw)
    {
        var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(raw)) return dict;
        if (raw.StartsWith('?')) raw = raw[1..];
        foreach (var pair in raw.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = pair.IndexOf('=');
            var key = idx < 0 ? pair : pair[..idx];
            var val = idx < 0 ? "" : pair[(idx + 1)..];
            key = Uri.UnescapeDataString(key);
            val = Uri.UnescapeDataString(val);
            if (!dict.TryGetValue(key, out var list))
                dict[key] = list = new List<string>();
            list.Add(val);
        }
        return dict;
    }

    /// <summary>文件名清洗（与 sanitize_filename 对齐）。</summary>
    public static string SanitizeFilename(string filename, int maxLength = 80)
    {
        filename = filename.Replace("\n", " ").Replace("\r", " ");
        filename = Regex.Replace(filename, @"[<>:""/\\|?*#\x00-\x1f]", "_");
        filename = Regex.Replace(filename, @"_+", "_");
        filename = Regex.Replace(filename, @" +", " ");
        filename = filename.Trim(' ', '.', '_', '-');
        if (filename.Length > maxLength)
            filename = filename[..maxLength].TrimEnd(' ', '.', '_', '-');
        var stem = filename.Split('.', 2)[0];
        if (WindowsReserved.Contains(stem))
            filename = "_" + filename;
        if (filename.Length > maxLength)
            filename = filename[..maxLength];
        return string.IsNullOrEmpty(filename) ? "untitled" : filename;
    }
}

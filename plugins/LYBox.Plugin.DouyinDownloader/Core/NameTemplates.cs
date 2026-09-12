using LYBox.Plugin.DouyinDownloader.Models;
using LYBox.Plugin.DouyinDownloader.Utils;

namespace LYBox.Plugin.DouyinDownloader.Core;

/// <summary>模板渲染（对齐 utils/naming.py）。</summary>
public static class NameTemplates
{
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "id","title","author","author_id","date","year","month","day","time","hour","minute","second","timestamp","type","mode",
    };
    public const int MaxTemplateLength = 200;
    public const int RenderedMaxLength = 80;

    public static string Validate(string template)
    {
        if (string.IsNullOrWhiteSpace(template)) throw new ArgumentException("模板为空");
        if (template.Length > MaxTemplateLength) throw new ArgumentException($"模板长度超过 {MaxTemplateLength}");
        if (template.Contains('/') || template.Contains('\\')) throw new ArgumentException("模板禁止包含路径分隔符");
        var variables = System.Text.RegularExpressions.Regex.Matches(template, @"\{([a-zA-Z_][a-zA-Z0-9_]*)\}")
            .Select(m => m.Groups[1].Value).Distinct().ToList();
        if (variables.Count == 0) throw new ArgumentException("模板必须至少引用一个 {var}");
        var bad = variables.Where(v => !Allowed.Contains(v)).ToList();
        if (bad.Count > 0) throw new ArgumentException($"未知变量: {string.Join(",", bad)}");
        return template;
    }

    public static string Render(string template, IReadOnlyDictionary<string, string> ctx)
    {
        var raw = System.Text.RegularExpressions.Regex.Replace(template, @"\{([a-zA-Z_][a-zA-Z0-9_]*)\}", m =>
        {
            var k = m.Groups[1].Value;
            return ctx.TryGetValue(k, out var v) && !string.IsNullOrEmpty(v) ? v : "";
        });
        var cleaned = UrlParser.SanitizeFilename(raw, RenderedMaxLength);
        if (cleaned == "untitled")
            cleaned = UrlParser.SanitizeFilename($"{ctx.GetValueOrDefault("date")}_{ctx.GetValueOrDefault("id")}", RenderedMaxLength);
        return cleaned;
    }

    public static IReadOnlyDictionary<string, string> BuildAwemeContext(AwemeDetail a, string mode) =>
        BuildContext(a.AwemeId, a.Title, a.AuthorName, a.AuthorSecUid, a.CreateTime, string.IsNullOrEmpty(a.AwemeType) ? "video" : a.AwemeType, mode);

    public static IReadOnlyDictionary<string, string> BuildMixContext(string mixId, string title, string author, long ts, string mode) =>
        BuildContext(mixId, title, author, "", ts, "mix", mode);

    public static IReadOnlyDictionary<string, string> BuildMusicContext(string musicId, string title, string author, long ts, string mode) =>
        BuildContext("music_" + musicId, title, author, "", ts, "music", mode);

    public static IReadOnlyDictionary<string, string> BuildContext(string id, string title, string author, string secUid, long ts, string type, string mode)
    {
        string date = "", year = "", month = "", day = "", time = "", hour = "", minute = "", second = "";
        if (ts > 0)
        {
            try
            {
                var dt = DateTimeOffset.FromUnixTimeSeconds(ts).ToLocalTime().DateTime;
                date = dt.ToString("yyyy-MM-dd");
                year = dt.Year.ToString();
                month = dt.Month.ToString("D2");
                day = dt.Day.ToString("D2");
                time = dt.ToString("HHmm");
                hour = dt.Hour.ToString("D2");
                minute = dt.Minute.ToString("D2");
                second = dt.Second.ToString("D2");
            }
            catch { /* ignore */ }
        }
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["id"] = id,
            ["title"] = title ?? "no_title",
            ["author"] = author ?? "",
            ["author_id"] = secUid ?? "",
            ["date"] = date,
            ["year"] = year,
            ["month"] = month,
            ["day"] = day,
            ["time"] = time,
            ["hour"] = hour,
            ["minute"] = minute,
            ["second"] = second,
            ["timestamp"] = ts > 0 ? ts.ToString() : "",
            ["type"] = type,
            ["mode"] = mode,
        };
    }
}

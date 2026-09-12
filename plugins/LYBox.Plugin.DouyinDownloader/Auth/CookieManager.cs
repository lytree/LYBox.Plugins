using System.Text.Json;
using LYBox.Plugin.DouyinDownloader.Config;
using LYBox.Plugin.DouyinDownloader.Utils;

namespace LYBox.Plugin.DouyinDownloader.Auth;

/// <summary>Cookie 管理（与 auth/cookie_manager.py 等价）。</summary>
public sealed class CookieManager
{
    private static readonly string[] BlockList =
    {
        "sessionid","sessionid_ss","sid_tt","sid_guard",
        "uid_tt","uid_tt_ss","passport_auth_status","passport_auth_status_ss",
        "passport_assist_user","passport_auth_mix_state","passport_mfa_token","login_time",
    };

    private readonly string _file;
    private readonly object _lock = new();
    private Dictionary<string, string> _cookies = new();

    public CookieManager(PluginConfigStore paths)
    {
        _file = paths.ResolvePath("cookies.json");
        Load();
    }

    public IReadOnlyDictionary<string, string> Cookies
    {
        get { lock (_lock) return new Dictionary<string, string>(_cookies); }
    }

    public void Set(Dictionary<string, string> cookies)
    {
        lock (_lock)
        {
            _cookies = Sanitize(cookies);
            Save();
        }
    }

    public string CookieHeader()
    {
        lock (_lock)
        {
            return string.Join("; ", _cookies.Select(kv => $"{kv.Key}={kv.Value}"));
        }
    }

    public bool Validate()
    {
        var need = new[] { "ttwid", "odin_tt", "passport_csrf_token" };
        lock (_lock)
        {
            var missing = need.Where(k => !_cookies.TryGetValue(k, out var v) || string.IsNullOrEmpty(v)).ToArray();
            if (missing.Length > 0)
            {
                Logger.Warn($"Cookie 缺少必要键: {string.Join(", ", missing)}");
                return false;
            }
            return true;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _cookies = new();
            if (File.Exists(_file)) File.Delete(_file);
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_file)) return;
            var json = File.ReadAllText(_file);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (parsed != null) _cookies = Sanitize(parsed);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Cookie 读取失败: {ex.Message}");
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(_cookies, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Logger.Error($"Cookie 写入失败: {ex.Message}");
        }
    }

    private static Dictionary<string, string> Sanitize(Dictionary<string, string> cookies)
    {
        var dict = new Dictionary<string, string>();
        foreach (var (k, v) in cookies)
        {
            if (string.IsNullOrEmpty(k)) continue;
            if (BlockList.Contains(k, StringComparer.OrdinalIgnoreCase)) continue;
            if (v == null) continue;
            var clean = v.Trim();
            if (clean.Length == 0) continue;
            dict[k] = clean;
        }
        return dict;
    }
}

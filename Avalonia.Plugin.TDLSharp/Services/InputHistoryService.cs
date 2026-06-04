using System.Collections.Concurrent;
using System.Text.Json;

namespace Avalonia.Plugin.TDLSharp.Services;

/// <summary>
/// 轻量级输入历史持久化服务，基于 JSON 文件按 Key 分组存储。
/// </summary>
public sealed class InputHistoryService
{
    private static readonly Lazy<InputHistoryService> _instance = new(() => new());
    public static InputHistoryService Instance => _instance.Value;

    private readonly string _basePath;
    private readonly ConcurrentDictionary<string, Lock> _keyLocks = [];
    private readonly ConcurrentDictionary<string, List<string>> _cache = [];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private InputHistoryService()
    {
        _basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AvaloniaTemplate", "TDLSharp", "history");
        Directory.CreateDirectory(_basePath);
    }

    private Lock GetLock(string key) => _keyLocks.GetOrAdd(key, _ => new Lock());

    /// <summary>
    /// 获取指定 Key 的历史记录列表（最新的在前）。
    /// </summary>
    public List<string> GetHistory(string key, int maxCount = 50)
    {
        lock (GetLock(key))
        {
            var items = GetOrCreateList(key);
            return items.Take(maxCount).ToList();
        }
    }

    /// <summary>
    /// 添加一条历史记录。如果已存在则移到最前面。
    /// </summary>
    public void AddHistory(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        lock (GetLock(key))
        {
            var items = GetOrCreateList(key);

            items.Remove(value);
            items.Insert(0, value);

            if (items.Count > 100)
                items.RemoveRange(100, items.Count - 100);

            SaveToFile(key, items);
        }
    }

    /// <summary>
    /// 删除指定 Key 的某条历史记录。
    /// </summary>
    public void RemoveHistory(string key, string value)
    {
        lock (GetLock(key))
        {
            var items = GetOrCreateList(key);
            if (items.Remove(value))
                SaveToFile(key, items);
        }
    }

    /// <summary>
    /// 清除指定 Key 的所有历史记录。
    /// </summary>
    public void ClearHistory(string key)
    {
        lock (GetLock(key))
        {
            _cache[key] = [];
            var filePath = GetFilePath(key);
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    private List<string> GetOrCreateList(string key)
    {
        if (_cache.TryGetValue(key, out var list))
            return list;

        var items = LoadFromFile(key);
        _cache[key] = items;
        return items;
    }

    private List<string> LoadFromFile(string key)
    {
        var filePath = GetFilePath(key);

        if (!File.Exists(filePath))
            return [];

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void SaveToFile(string key, List<string> items)
    {
        var filePath = GetFilePath(key);
        try
        {
            var json = JsonSerializer.Serialize(items, JsonOptions);
            File.WriteAllText(filePath, json);
        }
        catch
        {
            // 静默失败，不影响主流程
        }
    }

    private string GetFilePath(string key)
    {
        // 将 key 中的非法文件名字符替换
        var safeName = string.Join("_", key.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_basePath, $"{safeName}.json");
    }
}

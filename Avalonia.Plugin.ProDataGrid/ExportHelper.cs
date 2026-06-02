using System.Text.Json;
using System.Text.Json.Serialization;

namespace Avalonia.Plugin.ProDataGrid;

/// <summary>
/// 数据导出工具类，将集合序列化为 JSON 并保存到文件。
/// </summary>
internal static class ExportHelper
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 将集合导出为 JSON 文件到用户文档目录。
    /// </summary>
    /// <returns>保存路径，失败时返回 null。</returns>
    public static string? ExportToJson<T>(IEnumerable<T> data, string fileName)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ProDataGrid_Exports");
            Directory.CreateDirectory(dir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            var fullPath = Path.Combine(dir, $"{nameWithoutExt}_{timestamp}{ext}");

            var json = JsonSerializer.Serialize(data, s_jsonOptions);
            File.WriteAllText(fullPath, json);

            return fullPath;
        }
        catch
        {
            return null;
        }
    }
}

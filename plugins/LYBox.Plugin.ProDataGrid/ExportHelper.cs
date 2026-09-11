using System.Text.Json;
using System.Text.Json.Serialization;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Services;

namespace LYBox.Plugin.ProDataGrid;

/// <summary>
/// 数据导出工具类，将集合序列化为 JSON 并保存到文件。
/// 存储位置：优先主体 Data/{PluginId}/Exports/（<see cref="IPluginDataDirectoryProvider"/>），
/// 不可用时回退到 %USERPROFILE%/Documents/ProDataGrid_Exports/。
/// </summary>
internal static class ExportHelper
{
    /// <summary>PluginId 必须与 csproj 中的 PluginId 保持一致。</summary>
    private const string PluginId = "0F2F7DB6-0E9B-D872-442F-2CBC3DAC1FA1";

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 将集合导出为 JSON 文件到主体 Data 目录的 Exports 子目录下。
    /// </summary>
    /// <returns>保存路径，失败时返回 null。</returns>
    public static string? ExportToJson<T>(IEnumerable<T> data, string fileName)
    {
        try
        {
            var dir = ResolveExportDirectory();

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

    private static string ResolveExportDirectory()
    {
        if (ServiceLocator.TryGetService<IPluginDataDirectoryProvider>(out var provider)
            && provider is not null)
        {
            return provider.GetSubDirectory(PluginId, "Exports");
        }
        // 旧版兼容路径：早期版本会把导出写到用户文档目录。
        var legacyDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "ProDataGrid_Exports");
        Directory.CreateDirectory(legacyDir);
        return legacyDir;
    }
}

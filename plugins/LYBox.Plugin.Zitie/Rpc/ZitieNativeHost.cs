using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace LYBox.Plugin.Zitie.Rpc;

/// <summary>
/// 原生对话框桥接：把当前插件窗口的 <see cref="TopLevel"/> 暴露给 <see cref="ZitieCommands"/>。
/// </summary>
/// <remarks>
/// 与宿主 <c>SystemCommands</c> 同一模式——<c>StorageProvider</c> 只能从活动的 TopLevel 取得，
/// 而 <c>[RpcCommand]</c> 类由源生成器经 DI 实例化，拿不到视觉树上下文，
/// 故由页面在 Loaded 时注册一次委托。
/// </remarks>
public static class ZitieNativeHost
{
    private static Func<TopLevel?>? _topLevelProvider;

    /// <summary>页面 Loaded 时调用，注册窗口上下文。重复注册安全。</summary>
    public static void Attach(TopLevel? topLevel)
        => _topLevelProvider = () => topLevel;

    /// <summary>页面 Unloaded 时调用，释放窗口引用，避免窗口销毁后被 RPC 持有。</summary>
    public static void Detach()
        => _topLevelProvider = null;

    /// <summary>取当前窗口的 StorageProvider；不可用时返回 null（调用方需降级处理）。</summary>
    internal static IStorageProvider? Storage
        => _topLevelProvider?.Invoke()?.StorageProvider;

    // ==================== 对话框 ====================

    internal static async Task<string?> PickSavePathAsync(
        string? fileName, string? filterName, string[]? extensions)
    {
        var storage = Storage;
        if (storage is null) return null;

        var options = new FilePickerSaveOptions
        {
            Title = "导出字帖",
            SuggestedFileName = SanitizeFileName(fileName),
            ShowOverwritePrompt = true,
        };

        if (BuildTypes(filterName, extensions) is { } types && types.Length > 0)
            options.FileTypeChoices = types;

        var file = await storage.SaveFilePickerAsync(options);
        return file?.TryGetLocalPath();
    }

    internal static async Task<string?> PickOpenPathAsync(
        string? title, string? filterName, string[]? extensions)
    {
        var storage = Storage;
        if (storage is null) return null;

        var options = new FilePickerOpenOptions
        {
            Title = string.IsNullOrWhiteSpace(title) ? "导入文本" : title,
            AllowMultiple = false,
        };

        if (BuildTypes(filterName, extensions) is { } types && types.Length > 0)
            options.FileTypeFilter = types;

        var files = await storage.OpenFilePickerAsync(options);
        if (files.Count == 0) return null;
        return files[0].TryGetLocalPath();
    }

    // ==================== 辅助 ====================

    private static FilePickerFileType[]? BuildTypes(string? name, string[]? extensions)
    {
        if (extensions is not { Length: > 0 }) return null;

        var clean = extensions
            .Select(static e => e.Trim().TrimStart('.'))
            .Where(static e => e.Length > 0)
            .Select(static e => "*." + e)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return clean.Length == 0
            ? null
            : [new FilePickerFileType(string.IsNullOrWhiteSpace(name) ? "文件" : name) { Patterns = clean }];
    }

    /// <summary>
    /// 清洗建议文件名：剥离路径成分与文件系统非法字符。
    /// 前端传的是「字帖_永和字.pdf」这类纯名字，但 URL 参数可注入任意内容，故仍需防御。
    /// </summary>
    private static string SanitizeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "字帖.pdf";

        var leaf = Path.GetFileName(name);
        var cleaned = new string(leaf.Where(static c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());
        return cleaned.Length == 0 ? "字帖.pdf" : cleaned;
    }
}

using System.IO;
using System.Text;
using LYBox.Plugin.Shared.Attributes;

namespace LYBox.Plugin.Zitie.Rpc;

/// <summary>
/// 字帖导出 / 导入的宿主原生命令。
/// </summary>
/// <remarks>
/// <para>
/// WebView2 / WPE WebKit 内嵌浏览器没有可靠的 blob 下载落地路径
/// （<c>a[download]</c> 常静默失败），且 <c>&lt;input type="file"&gt;</c> 的
/// 文件选择由引擎自行处理、样式与行为不可控。因此把「选路径 → 写盘 / 读盘」
/// 整条链路下沉到宿主，复用 Avalonia StorageProvider 的原生对话框，
/// 与平台主题一致。
/// </para>
/// <para>
/// 打印：Avalonia 无跨平台打印对话框（Windows 需 <c>System.Drawing.Printing</c>，
/// Linux/macOS 无等价 API）。故采用「生成 PDF → 用系统默认程序打开」，
/// 由用户在阅读器中打印。跨平台行为一致。
/// </para>
/// <para>
/// 前端调用（经 <c>/sdk/lybox-plugin-sdk.js</c> 的 <c>invoke</c>）：
/// <code>
/// await invoke('saveBytes', { fileName, base64, filterName, extensions });
/// await invoke('readTextFile', { fileName, base64 });
/// await invoke('openPath',   { path });
/// </code>
/// </para>
/// </remarks>
public partial class ZitieCommands
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>单次 RPC 的字节上限。A4 300dpi 全页 PNG 约 25MB，留足余量。</summary>
    private const int MaxBytes = 96 * 1024 * 1024;

    /// <summary>UTF-8 解码失败时产生的替换字符 U+FFFD，用作「不是 UTF-8」的判据。</summary>
    private const char ReplacementChar = '\uFFFD';

    /// <summary>
    /// 保存二进制文件（导出 PDF / PNG）。前端传 base64，宿主弹原生保存对话框后写盘。
    /// </summary>
    /// <returns>实际写入的路径；用户取消返回 null。</returns>
    [RpcCommand]
    public async Task<string?> SaveBytesAsync(SaveBytesRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var bytes = DecodeBase64(request.Base64);
        if (bytes.Length > MaxBytes || bytes.Length == 0)
            return null;

        var path = await ZitieNativeHost
            .PickSavePathAsync(request.FileName, request.FilterName, request.Extensions)
            .ConfigureAwait(true);
        if (path is null) return null;

        ct.ThrowIfCancellationRequested();
        await File.WriteAllBytesAsync(path, bytes, ct).ConfigureAwait(true);
        return path;
    }

    /// <summary>
    /// 读取本地文本文件（TXT 导入）。宿主完成对话框与读盘，UTF-8 优先、含替换字符时回退 GBK。
    /// </summary>
    /// <returns>文本内容与文件名；用户取消或失败返回 null。</returns>
    [RpcCommand]
    public async Task<ImportedText?> ReadTextFileAsync(ReadTextRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var path = await ZitieNativeHost
            .PickOpenPathAsync(request.FileName, request.FilterName, request.Extensions)
            .ConfigureAwait(true);
        if (path is null) return null;

        ct.ThrowIfCancellationRequested();

        // 与前端 readTextFile 同策略：UTF-8 优先，出现 U+FFFD 说明不是 UTF-8，回退 GBK。
        var buffer = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(true);
        var text = Utf8.GetString(RemoveBom(buffer));
        if (text.Contains(ReplacementChar) && DecodeGbk(buffer) is { } gbk)
            text = gbk;

        return new ImportedText(text, Path.GetFileName(path));
    }

    /// <summary>
    /// 用系统默认程序打开文件 / 路径。打印流程的最后一步（PDF 落盘后交给阅读器打印）。
    /// </summary>
    /// <returns>是否成功交给系统。</returns>
    [RpcCommand]
    public Task<bool> OpenPathAsync(OpenPathRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return string.IsNullOrWhiteSpace(request.Path)
            ? Task.FromResult(false)
            : OpenWithShellAsync(request.Path);
    }

    // ==================== 平台实现 ====================

    private static async Task<bool> OpenWithShellAsync(string path)
    {
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return false;

            if (OperatingSystem.IsWindows())
            {
                // start "" 打开：空标题参数避免路径含 & 时被截断；UseShellExecute 让系统选默认程序。
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                });
                return true;
            }

            if (OperatingSystem.IsMacOS())
            {
                System.Diagnostics.Process.Start("open", $"/{path}")?.WaitForExit(5000);
                return true;
            }

            // Linux / 其他：xdg-open 走 freedesktop 规范，绝大多数桌面环境可用。
            var psi = new System.Diagnostics.ProcessStartInfo("xdg-open", path) { UseShellExecute = false };
            using var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(5000);
            return proc is not null;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] DecodeBase64(string? base64)
    {
        if (string.IsNullOrEmpty(base64)) return [];
        var clean = base64.AsSpan();
        // 前端 toDataURL / atob 产物可能带 data URL 前缀，统一剥掉。
        var comma = clean.IndexOf(',');
        if (clean.StartsWith("data:", StringComparison.Ordinal) && comma > 0)
            clean = clean[(comma + 1)..];

        try
        {
            return Convert.FromBase64String(clean.ToString());
        }
        catch (FormatException)
        {
            return [];
        }
    }

    private static string? DecodeGbk(byte[] buffer)
    {
        try
        {
            // .NET Core 默认不含 GBK 代码页，需先注册提供程序。
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var gbk = Encoding.GetEncoding(936);
            return gbk.GetString(RemoveBom(buffer));
        }
        catch
        {
            return null;
        }
    }

    private static byte[] RemoveBom(byte[] buffer)
        => buffer.Length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF
            ? buffer[3..]
            : buffer;
}

// ==================== DTO ====================

/// <summary>保存二进制文件的请求。多参数必须打包为单个 DTO（源生成器约束）。</summary>
public sealed record SaveBytesRequest(
    /// <summary>建议文件名（含扩展名），如 字帖_永和字.pdf。</summary>
    string FileName,
    /// <summary>文件内容的 base64（可带 data URL 前缀）。</summary>
    string Base64,
    /// <summary>对话框过滤器名，如 "PDF 文件"。</summary>
    string? FilterName = null,
    /// <summary>过滤器扩展名，如 ["pdf"]。为空则不加扩展名过滤器。</summary>
    string[]? Extensions = null);

/// <summary>读取文本文件的请求。</summary>
public sealed record ReadTextRequest(
    /// <summary>对话框标题，如 "导入 TXT"。</summary>
    string? FileName = null,
    /// <summary>过滤器名。</summary>
    string? FilterName = null,
    /// <summary>过滤器扩展名。</summary>
    string[]? Extensions = null);

/// <summary>导入的文本内容。</summary>
public sealed record ImportedText(string Text, string FileName);

/// <summary>用系统默认程序打开路径的请求。</summary>
public sealed record OpenPathRequest(string Path);

using System.Collections.ObjectModel;
using Avalonia.Platform.Storage;
using Avalonia.Plugin.Downloader.Models;
using Avalonia.Plugin.Downloader.Services;
using Avalonia.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avalonia.Plugin.Downloader.ViewModels;

[NavigationItem("Downloader_M3u8")]
[Menu("NAV_Downloader_M3u8", "Downloader_M3u8", ParentKey = "NAV_Downloader", Order = 1)]
[ViewMap(typeof(Pages.M3u8DownloaderPage))]
public partial class M3u8DownloaderViewModel : DownloaderViewModelBase
{
    [ObservableProperty] private string _savePath = string.Empty;
    [ObservableProperty] private ObservableCollection<M3u8UrlEntry> _urlEntries = [];

    public override ScriptDescriptor Script => new()
    {
        Id = "m3u8-downloader",
        Name = "M3U8 下载器",
        Description = "下载 M3U8 视频流，支持 AES-128/AES-128-ECB/CHACHA20 加密解密及 FFmpeg 合并",
        Parameters =
        [
            ScriptParameter.Number("concurrency", "并发数", "同时下载分片数", 8),
            ScriptParameter.Text("quality", "画质选择", "best/worst/分辨率/带宽", "best"),
            ScriptParameter.Text("headers", "HTTP 请求头", "格式: key=value (多个用逗号分隔)"),
            ScriptParameter.Text("ffmpegPath", "FFmpeg 路径", "ffmpeg 可执行文件路径", "ffmpeg"),
            ScriptParameter.Number("retry", "重试次数", "失败分片重试次数", 3),
        ]
    };

    public M3u8DownloaderViewModel()
    {
        UrlEntries.Add(new M3u8UrlEntry());
    }

    [RelayCommand]
    private void AddUrlEntry()
    {
        UrlEntries.Add(new M3u8UrlEntry());
    }

    [RelayCommand]
    private void RemoveUrlEntry(M3u8UrlEntry entry)
    {
        UrlEntries.Remove(entry);
    }

    [RelayCommand]
    private async Task BrowseSavePath()
    {
        var topLevel = Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (topLevel == null) return;

        var storageProvider = topLevel.StorageProvider;

        var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择保存路径",
            AllowMultiple = false
        });

        if (result.Count > 0)
        {
            SavePath = result[0].TryGetLocalPath() ?? result[0].Path.ToString();
        }
    }

    protected override async Task ExecuteCoreAsync(Dictionary<string, string> paramValues, CancellationToken ct)
    {
        var logger = CreateUiLogger();
        var service = new M3u8DownloadService(logger);

        var headers = ParseHeaders(paramValues.GetValueOrDefault("headers", ""));
        var concurrency = int.TryParse(paramValues.GetValueOrDefault("concurrency", "8"), out var c) ? c : 8;
        var quality = paramValues.GetValueOrDefault("quality", "best");
        var ffmpegPath = paramValues.GetValueOrDefault("ffmpegPath", "ffmpeg");
        var retryCount = int.TryParse(paramValues.GetValueOrDefault("retry", "3"), out var r) ? r : 3;

        var validEntries = UrlEntries
            .Where(e => !string.IsNullOrWhiteSpace(e.Url))
            .ToList();

        if (validEntries.Count == 0)
        {
            logger.Log("未输入有效的 M3U8 链接");
            return;
        }

        var saveDir = string.IsNullOrWhiteSpace(SavePath) ? Environment.CurrentDirectory : SavePath;

        for (int i = 0; i < validEntries.Count; i++)
        {
            var entry = validEntries[i];
            var outputFileName = entry.EffectiveFileName;
            var outputPath = Path.Combine(saveDir, outputFileName);

            if (validEntries.Count > 1)
            {
                logger.Log($"[{i + 1}/{validEntries.Count}] 正在下载: {entry.Url}");
            }

            await service.DownloadAsync(
                url: entry.Url,
                output: outputPath,
                concurrency: concurrency,
                quality: quality,
                headers: headers,
                ffmpegPath: ffmpegPath,
                retryCount: retryCount,
                ct: ct);
        }
    }

    private static Dictionary<string, string>? ParseHeaders(string? headerStr)
    {
        if (string.IsNullOrWhiteSpace(headerStr)) return null;

        var headers = new Dictionary<string, string>();
        foreach (var pair in headerStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = pair.IndexOf('=');
            if (idx > 0)
            {
                var key = pair[..idx];
                var value = pair[(idx + 1)..];
                headers[key] = value;
            }
        }
        return headers.Count > 0 ? headers : null;
    }
}

using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Platform.Storage;
using LYBox.Plugin.Downloader.Models;
using LYBox.Plugin.Downloader.Resources;
using LYBox.Plugin.Downloader.Services;
using LYBox.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LYBox.Plugin.Downloader.ViewModels;

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
        Name = Strings.Get("SCRIPT_M3u8_Name"),
        Description = Strings.Get("SCRIPT_M3u8_Desc"),
        Parameters =
        [
            ScriptParameter.Number("concurrency", Strings.Get("PARAM_Concurrency"), Strings.Get("PARAM_ConcurrencyDesc"), 8),
            ScriptParameter.Text("quality", Strings.Get("PARAM_Quality"), Strings.Get("PARAM_QualityDesc"), "best"),
            ScriptParameter.Text("headers", Strings.Get("PARAM_Headers"), Strings.Get("PARAM_HeadersDesc")),
            ScriptParameter.Text("ffmpegPath", Strings.Get("PARAM_FFmpegPath"), Strings.Get("PARAM_FFmpegPathDesc"), "ffmpeg"),
            ScriptParameter.Number("retry", Strings.Get("PARAM_Retry"), Strings.Get("PARAM_RetryDesc"), 3),
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
            Title = Strings.Get("DIALOG_SelectSavePath"),
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
            logger.Log(Strings.Get("MSG_NoValidM3u8Url"));
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
                logger.Log(Strings.Get("FMT_Downloading", i + 1, validEntries.Count, entry.Url));
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

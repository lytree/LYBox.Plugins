using CommunityToolkit.Mvvm.ComponentModel;

namespace LYBox.Plugin.Downloader.Models;

public partial class M3u8UrlEntry : ObservableObject
{
    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private string _fileName = string.Empty;

    public string EffectiveFileName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(FileName))
                return FileName;

            return ExtractFileNameFromUrl(Url);
        }
    }

    private static string ExtractFileNameFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "output.mp4";

        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            var lastSegment = path.Split('/').LastOrDefault() ?? "";

            var nameWithoutExt = lastSegment;
            var dotIndex = nameWithoutExt.LastIndexOf('.');
            if (dotIndex > 0)
            {
                nameWithoutExt = nameWithoutExt[..dotIndex];
            }

            if (string.IsNullOrWhiteSpace(nameWithoutExt) || nameWithoutExt.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
            {
                nameWithoutExt = $"video_{DateTime.Now:yyyyMMdd_HHmmss}";
            }

            return $"{nameWithoutExt}.mp4";
        }
        catch
        {
            return "output.mp4";
        }
    }
}

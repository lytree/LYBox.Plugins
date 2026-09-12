using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LYBox.Plugin.DouyinDownloader.Models;
using LYBox.Plugin.DouyinDownloader.Resources;
using LYBox.Plugin.DouyinDownloader.Services;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;

namespace LYBox.Plugin.DouyinDownloader.ViewModels;

[NavigationItem("Douyin_Submit")]
[Menu("NAV_DouyinSubmit", "Douyin_Submit", ParentKey = "NAV_DouyinRoot", Order = 1)]
[ViewMap(typeof(Pages.SubmitPage))]
public partial class SubmitViewModel : ViewModelBase
{
    private readonly DownloadCoordinator _coord;

    [ObservableProperty] private string _url = "";
    [ObservableProperty] private string _selectedMode = "post";
    [ObservableProperty] private int _number = 0;
    [ObservableProperty] private string _statusText = "请粘贴抖音链接";
    [ObservableProperty] private string _startDate = "";
    [ObservableProperty] private string _endDate = "";

    public string[] Modes { get; } = { "post", "like", "mix", "collect", "collectmix" };

    public SubmitViewModel()
    {
        _coord = ServiceLocator.TryGetService<DownloadCoordinator>(out var svc) ? svc! : throw new InvalidOperationException("DownloadCoordinator not registered");
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(Url))
        {
            StatusText = "URL 不能为空"; return;
        }
        StatusText = "提交中…";
        var mode = Enum.TryParse<DownloadMode>(SelectedMode, true, out var m) ? m : DownloadMode.Post;
        var (ok, msg, jobId) = await _coord.SubmitAsync(Url.Trim(), mode, Number,
            TryParseDate(StartDate), TryParseDate(EndDate));
        StatusText = ok ? $"已入队: {jobId?.ToString().Substring(0, 8)}…" : (msg ?? "失败");
    }

    private static DateTimeOffset? TryParseDate(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTimeOffset.TryParse(s, out var d)) return d;
        return null;
    }
}

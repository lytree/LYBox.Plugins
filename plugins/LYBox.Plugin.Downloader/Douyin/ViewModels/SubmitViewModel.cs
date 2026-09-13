using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LYBox.Plugin.Downloader.Douyin.Models;
using LYBox.Plugin.Downloader.Douyin.Services;
using LYBox.Plugin.Shared;

namespace LYBox.Plugin.Downloader.Douyin.ViewModels;

/// <summary>
/// 提交下载 ViewModel。作为 DouyinHomePage 的 Tab 内容使用。
/// </summary>
public partial class SubmitViewModel : ViewModelBase
{
    private readonly DownloadCoordinator? _coord;

    [ObservableProperty] private string _url = "";
    [ObservableProperty] private string _selectedMode = "post";
    [ObservableProperty] private int _number = 0;
    [ObservableProperty] private string _statusText = "请粘贴抖音链接";
    [ObservableProperty] private string _startDate = "";
    [ObservableProperty] private string _endDate = "";

    public string[] Modes { get; } = { "post", "like", "mix", "collect", "collectmix" };

    public SubmitViewModel()
    {
        if (!ServiceLocator.TryGetService<DownloadCoordinator>(out var svc) || svc is null)
        {
            StatusText = "DownloadCoordinator 服务解析失败 — 请查看应用日志";
            return;
        }
        _coord = svc;
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (_coord is null)
        {
            StatusText = "服务未就绪，无法提交";
            return;
        }
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

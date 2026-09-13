using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LYBox.Plugin.Downloader.Douyin.Storage;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;

namespace LYBox.Plugin.Downloader.Douyin.ViewModels;

[NavigationItem("Douyin_History")]
[Menu("NAV_DouyinHistory", "Douyin_History", ParentKey = "NAV_DouyinRoot", Order = 3)]
[ViewMap(typeof(Pages.HistoryPage))]
public partial class HistoryViewModel : ViewModelBase
{
    private readonly DownloadDatabase? _db;
    public ObservableCollection<HistoryRow> History { get; } = new();

    [ObservableProperty] private string _statusText = "";

    public HistoryViewModel()
    {
        if (!ServiceLocator.TryGetService<DownloadDatabase>(out var svc) || svc is null)
        {
            // 服务解析失败：DI 注入异常或 ServiceLocator 未初始化。降级为只读错误状态而非崩溃宿主。
            StatusText = "DownloadDatabase 服务解析失败 — 请查看应用日志";
            return;
        }
        _db = svc;
        try { Refresh(); }
        catch (Exception ex) { StatusText = $"加载历史失败: {ex.Message}"; }
    }

    public void Activate()
    {
        if (_db is null) return;
        Refresh();
    }

    [RelayCommand]
    public void Refresh()
    {
        if (_db is null) return;
        History.Clear();
        foreach (var r in _db.ListHistory(500)) History.Add(r);
        StatusText = $"共 {History.Count} 条历史记录";
    }

    [RelayCommand]
    private void OpenFile(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "open",
            });
        }
        catch { }
    }
}

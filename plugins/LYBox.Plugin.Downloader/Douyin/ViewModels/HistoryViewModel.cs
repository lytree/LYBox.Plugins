using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LYBox.Plugin.Downloader.Douyin.Storage;
using LYBox.Plugin.Shared;

namespace LYBox.Plugin.Downloader.Douyin.ViewModels;

/// <summary>
/// 历史记录 ViewModel。作为 DouyinHomePage 的 Tab 内容使用。
/// </summary>
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

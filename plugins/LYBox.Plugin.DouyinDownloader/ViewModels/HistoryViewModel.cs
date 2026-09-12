using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LYBox.Plugin.DouyinDownloader.Storage;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;

namespace LYBox.Plugin.DouyinDownloader.ViewModels;

[NavigationItem("Douyin_History")]
[Menu("NAV_DouyinHistory", "Douyin_History", ParentKey = "NAV_DouyinRoot", Order = 3)]
[ViewMap(typeof(Pages.HistoryPage))]
public partial class HistoryViewModel : ViewModelBase
{
    private readonly DownloadDatabase _db;
    public ObservableCollection<HistoryRow> History { get; } = new();

    [ObservableProperty] private string _statusText = "";

    public HistoryViewModel()
    {
        _db = ServiceLocator.TryGetService<DownloadDatabase>(out var svc) ? svc! : throw new InvalidOperationException();
        Refresh();
    }

    public void Activate() => Refresh();

    [RelayCommand]
    public void Refresh()
    {
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

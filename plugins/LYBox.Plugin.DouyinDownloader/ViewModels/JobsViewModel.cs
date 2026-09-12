using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LYBox.Plugin.DouyinDownloader.Models;
using LYBox.Plugin.DouyinDownloader.Services;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;

namespace LYBox.Plugin.DouyinDownloader.ViewModels;

[NavigationItem("Douyin_Jobs")]
[Menu("NAV_DouyinJobs", "Douyin_Jobs", ParentKey = "NAV_DouyinRoot", Order = 2)]
[ViewMap(typeof(Pages.JobsPage))]
public partial class JobsViewModel : ViewModelBase
{
    private readonly DownloadCoordinator _coord;
    public ObservableCollection<DownloadJobRow> Jobs { get; } = new();

    [ObservableProperty] private string _statusText = "暂无任务";

    public ICommand RefreshCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand OpenOutputDirCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand ResumeCommand { get; }

    public JobsViewModel()
    {
        _coord = ServiceLocator.TryGetService<DownloadCoordinator>(out var svc) ? svc! : throw new InvalidOperationException();
        RefreshCommand = new RelayCommand(Refresh);
        RemoveCommand = new RelayCommand<Guid>(Remove);
        OpenOutputDirCommand = new RelayCommand<DownloadJobRow>(OpenOutputDir);
        PauseCommand = new RelayCommand<Guid>(Pause, j => CanPause(j));
        ResumeCommand = new RelayCommand<Guid>(async j => await ResumeAsync(j), j => CanResume(j));
        Refresh();
        _ = PollLoop();
    }

    public void Activate() => Refresh();

    /// <summary>后台 1s 轮询刷新进度（仅 UI 触发,可被宿主管控）。</summary>
    private async Task PollLoop()
    {
        while (!IsDisposed)
        {
            try { await Task.Delay(1000); } catch { return; }
            try
            {
                foreach (var j in _coord.ListJobs())
                {
                    var idx = Jobs.IndexOf(j);
                    if (idx >= 0) Jobs[idx] = j;
                }
                StatusText = $"任务总数: {Jobs.Count}";
                ((RelayCommand<Guid>)PauseCommand).NotifyCanExecuteChanged();
                ((RelayCommand<Guid>)ResumeCommand).NotifyCanExecuteChanged();
            }
            catch { }
        }
    }

    public void Refresh()
    {
        Jobs.Clear();
        foreach (var j in _coord.ListJobs()) Jobs.Add(j);
        StatusText = $"任务总数: {Jobs.Count}";
    }

    private bool CanPause(Guid jobId)
    {
        var row = _coord.GetJob(jobId);
        return row != null && row.Status == JobStatus.Running && row.UrlKind == UrlKind.Live;
    }

    private bool CanResume(Guid jobId)
    {
        var row = _coord.GetJob(jobId);
        return row != null && row.Status == JobStatus.Paused && row.UrlKind == UrlKind.Live;
    }

    private void Pause(Guid jobId)
    {
        if (_coord.Pause(jobId)) StatusText = "已请求暂停";
        Refresh();
    }

    private async Task ResumeAsync(Guid jobId)
    {
        var result = await _coord.ResumeAsync(jobId);
        if (result != null) StatusText = $"已恢复 ({result.Bytes} 字节, stop_reason={result.StopReason})";
        Refresh();
    }

    private void Remove(Guid jobId)
    {
        _coord.Remove(jobId);
        Refresh();
    }

    private void OpenOutputDir(DownloadJobRow? row)
    {
        if (row == null || string.IsNullOrEmpty(row.OutputDir)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = row.OutputDir,
                UseShellExecute = true,
                Verb = "open",
            });
        }
        catch { }
    }

    private bool _isDisposed;
    private new bool IsDisposed => _isDisposed;
    public override void Dispose()
    {
        _isDisposed = true;
        base.Dispose();
    }
}

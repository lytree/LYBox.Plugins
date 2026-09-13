using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LYBox.Plugin.Downloader.Models;
using LYBox.Plugin.Downloader.Services;
using LYBox.Plugin.Shared;

namespace LYBox.Plugin.Downloader.ViewModels;

/// <summary>
/// 任务中心 ViewModel。作为 DouyinHomePage 的 Tab 内容使用。
/// </summary>
public partial class JobsViewModel : ViewModelBase
{
    private readonly DownloadCoordinator? _coord;
    public ObservableCollection<DownloadJobRow> Jobs { get; } = new();

    [ObservableProperty] private string _statusText = "暂无任务";

    public ICommand RefreshCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand OpenOutputDirCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand ResumeCommand { get; }

    public JobsViewModel()
    {
        _coord = ServiceLocator.TryGetService<DownloadCoordinator>(out var svc) ? svc : null;
        if (_coord is null)
        {
            StatusText = "DownloadCoordinator 服务解析失败 — 请查看应用日志";
            // 仍然初始化 RelayCommand,但 CanExecute 永远返回 false,避免 XAML 绑定空属性触发 NRE
            RefreshCommand = new RelayCommand(() => { }, () => false);
            RemoveCommand = new RelayCommand<Guid>(_ => { }, _ => false);
            OpenOutputDirCommand = new RelayCommand<DownloadJobRow>(_ => { }, _ => false);
            PauseCommand = new RelayCommand<Guid>(_ => { }, _ => false);
            ResumeCommand = new RelayCommand<Guid>(async _ => { }, _ => false);
            return;
        }
        RefreshCommand = new RelayCommand(Refresh);
        RemoveCommand = new RelayCommand<Guid>(Remove);
        OpenOutputDirCommand = new RelayCommand<DownloadJobRow>(OpenOutputDir);
        PauseCommand = new RelayCommand<Guid>(Pause, j => CanPause(j));
        ResumeCommand = new RelayCommand<Guid>(async j => await ResumeAsync(j), j => CanResume(j));
        Refresh();
        _ = PollLoop();
    }

    /// <summary>后台 1s 轮询刷新进度（仅 UI 触发,可被宿主管控）。</summary>
    private async Task PollLoop()
    {
        while (!IsDisposed)
        {
            try { await Task.Delay(1000); } catch { return; }
            if (_coord is null) return;
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
        if (_coord is null) return;
        Jobs.Clear();
        foreach (var j in _coord.ListJobs()) Jobs.Add(j);
        StatusText = $"任务总数: {Jobs.Count}";
    }

    private bool CanPause(Guid jobId)
    {
        if (_coord is null) return false;
        var row = _coord.GetJob(jobId);
        return row != null && row.Status == JobStatus.Running && row.UrlKind == UrlKind.Live;
    }

    private bool CanResume(Guid jobId)
    {
        if (_coord is null) return false;
        var row = _coord.GetJob(jobId);
        return row != null && row.Status == JobStatus.Paused && row.UrlKind == UrlKind.Live;
    }

    private void Pause(Guid jobId)
    {
        if (_coord is null) return;
        if (_coord.Pause(jobId)) StatusText = "已请求暂停";
        Refresh();
    }

    private async Task ResumeAsync(Guid jobId)
    {
        if (_coord is null) return;
        var result = await _coord.ResumeAsync(jobId);
        if (result != null) StatusText = $"已恢复 ({result.Bytes} 字节, stop_reason={result.StopReason})";
        Refresh();
    }

    private void Remove(Guid jobId)
    {
        if (_coord is null) return;
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
}

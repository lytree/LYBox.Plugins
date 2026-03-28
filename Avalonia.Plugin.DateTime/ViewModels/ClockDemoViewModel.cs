using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Timers;

namespace Avalonia.Plugin.DateTimeControls.ViewModels;

[Menu("Clock", MenuKeys.MenuKeyClock)]
public partial class ClockDemoViewModel: ObservableObject, IDisposable
{
    private System.Timers.Timer _timer;
    
    [ObservableProperty] 
    private DateTimeOffset _time;
    public ClockDemoViewModel()
    {
        Time = DateTimeOffset.Now;
        _timer = new System.Timers.Timer(1000);
        _timer.Elapsed += TimerOnElapsed;
        _timer.Start();
    }

    private void TimerOnElapsed(object? sender, ElapsedEventArgs e)
    {
        Time = DateTimeOffset.Now;
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Elapsed -= TimerOnElapsed;
        _timer.Dispose();
    }
}






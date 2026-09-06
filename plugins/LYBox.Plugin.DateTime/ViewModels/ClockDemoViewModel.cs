using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.DateTimeControls.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Timers;

namespace LYBox.Plugin.DateTimeControls.ViewModels;

[NavigationItem("KeyClock")]
[Menu("NAV_Clock", "KeyClock", "NAV_DateTime")]
[ViewMap(typeof(ClockDemo))]
public partial class ClockDemoViewModel: ObservableObject, IDisposable
{
    private System.Timers.Timer _timer;
    
    [ObservableProperty] 
    private DateTime _time;
    public ClockDemoViewModel()
    {
        Time = DateTime.Now;
        _timer = new System.Timers.Timer(1000);
        _timer.Elapsed += TimerOnElapsed;
        _timer.Start();
    }

    private void TimerOnElapsed(object? sender, ElapsedEventArgs e)
    {
        Time = DateTime.Now;
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Elapsed -= TimerOnElapsed;
        _timer.Dispose();
    }
}






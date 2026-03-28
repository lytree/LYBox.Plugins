using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.DateTimeControls.ViewModels;


public partial class TimePickerDemoViewModel: ObservableObject
{
    [ObservableProperty] private TimeSpan? _time;
    
    public TimePickerDemoViewModel()
    {
        Time = new TimeSpan(12, 20, 0);
    }
}






using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.DateTimeControls.Pages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.DateTimeControls.ViewModels;

[NavigationItem("KeyTimeRangePicker")]
[Menu("Time Range Picker", "KeyTimeRangePicker", "Date & Time")]
[ViewMap(typeof(TimeRangePickerDemo))]
public partial class TimeRangePickerDemoViewModel: ObservableObject
{
    [ObservableProperty] private TimeSpan? _startTime;
    [ObservableProperty] private TimeSpan? _endTime;

    public TimeRangePickerDemoViewModel()
    {
        StartTime = new TimeSpan(8, 21, 0);
        EndTime = new TimeSpan(18, 22, 0);
    }
}






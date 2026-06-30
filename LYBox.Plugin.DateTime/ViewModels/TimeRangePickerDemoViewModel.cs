using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.DateTimeControls.Pages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LYBox.Plugin.DateTimeControls.ViewModels;

[NavigationItem("KeyTimeRangePicker")]
[Menu("NAV_TimeRangePicker", "KeyTimeRangePicker", "NAV_DateTime")]
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






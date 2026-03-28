using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.DateTimeControls.ViewModels;

[Menu("TimeRangePicker", MenuKeys.MenuKeyTimeRangePicker)]
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






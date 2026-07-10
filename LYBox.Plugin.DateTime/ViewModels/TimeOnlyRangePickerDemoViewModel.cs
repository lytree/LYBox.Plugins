using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.DateTimeControls.Pages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LYBox.Plugin.DateTimeControls.ViewModels;

[NavigationItem("KeyTimeOnlyRangePicker")]
[Menu("NAV_TimeOnlyRangePicker", "KeyTimeOnlyRangePicker", "NAV_DateTime")]
[ViewMap(typeof(TimeOnlyRangePickerDemo))]
public partial class TimeOnlyRangePickerDemoViewModel : ObservableObject
{
    [ObservableProperty] private System.TimeOnly? _startTime;
    [ObservableProperty] private System.TimeOnly? _endTime;

    public TimeOnlyRangePickerDemoViewModel()
    {
        StartTime = new System.TimeOnly(8, 21, 0);
        EndTime = new System.TimeOnly(18, 22, 0);
    }
}

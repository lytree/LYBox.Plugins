using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.DateTimeControls.Pages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LYBox.Plugin.DateTimeControls.ViewModels;

[NavigationItem("KeyDateOffsetRangePicker")]
[Menu("NAV_DateOffsetRangePicker", "KeyDateOffsetRangePicker", "NAV_DateTime")]
[ViewMap(typeof(DateOffsetRangePickerDemo))]
public partial class DateOffsetRangePickerDemoViewModel : ObservableObject
{
    [ObservableProperty] private System.DateTimeOffset? _startDate;
    [ObservableProperty] private System.DateTimeOffset? _endDate;

    public DateOffsetRangePickerDemoViewModel()
    {
        StartDate = System.DateTimeOffset.Now;
        EndDate = System.DateTimeOffset.Now.AddDays(7);
    }
}

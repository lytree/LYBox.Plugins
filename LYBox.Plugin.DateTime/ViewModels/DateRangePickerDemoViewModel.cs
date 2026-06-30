using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.DateTimeControls.Pages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LYBox.Plugin.DateTimeControls.ViewModels;

[NavigationItem("KeyDateRangePicker")]
[Menu("NAV_DateRangePicker", "KeyDateRangePicker", "NAV_DateTime")]
[ViewMap(typeof(DateRangePickerDemo))]
public partial class DateRangePickerDemoViewModel: ObservableObject
{
    [ObservableProperty] private System.DateTime? _startDate;
    [ObservableProperty] private System.DateTime? _endDate;

    public DateRangePickerDemoViewModel()
    {
        StartDate = System.DateTime.Today;
        EndDate = System.DateTime.Today.AddDays(7);
    }
}






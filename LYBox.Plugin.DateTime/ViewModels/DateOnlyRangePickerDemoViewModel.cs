using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.DateTimeControls.Pages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LYBox.Plugin.DateTimeControls.ViewModels;

[NavigationItem("KeyDateOnlyRangePicker")]
[Menu("NAV_DateOnlyRangePicker", "KeyDateOnlyRangePicker", "NAV_DateTime")]
[ViewMap(typeof(DateOnlyRangePickerDemo))]
public partial class DateOnlyRangePickerDemoViewModel : ObservableObject
{
    [ObservableProperty] private System.DateOnly? _startDate;
    [ObservableProperty] private System.DateOnly? _endDate;

    public DateOnlyRangePickerDemoViewModel()
    {
        StartDate = System.DateOnly.FromDateTime(System.DateTime.Today);
        EndDate = System.DateOnly.FromDateTime(System.DateTime.Today.AddDays(7));
    }
}

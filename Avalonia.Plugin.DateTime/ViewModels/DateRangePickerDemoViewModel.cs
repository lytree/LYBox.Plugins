using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.DateTimeControls.ViewModels;

[Menu("DateRangePicker", MenuKeys.MenuKeyDateRangePicker)]
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






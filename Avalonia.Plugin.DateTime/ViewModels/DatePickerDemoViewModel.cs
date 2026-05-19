using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.DateTimeControls.Pages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.DateTimeControls.ViewModels;

[NavigationItem("KeyDatePicker")]
[Menu("NAV_DatePicker", "KeyDatePicker", "NAV_DateTime")]
[ViewMap(typeof(DatePickerDemo))]
public partial class DatePickerDemoViewModel: ObservableObject
{
    [ObservableProperty] private System.DateTime? _selectedDate;

    public DatePickerDemoViewModel()
    {
        SelectedDate = System.DateTime.Today;
    }
}






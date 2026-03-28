using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.DateTimeControls.ViewModels;

[Menu("DatePicker", MenuKeys.MenuKeyDatePicker)]
public partial class DatePickerDemoViewModel: ObservableObject
{
    [ObservableProperty] private System.DateTime? _selectedDate;

    public DatePickerDemoViewModel()
    {
        SelectedDate = System.DateTime.Today;
    }
}






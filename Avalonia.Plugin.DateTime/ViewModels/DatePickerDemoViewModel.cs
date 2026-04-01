using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.DateTimeControls.ViewModels;


public partial class DatePickerDemoViewModel: ObservableObject
{
    [ObservableProperty] private System.DateTime? _selectedDate;

    public DatePickerDemoViewModel()
    {
        SelectedDate = System.DateTime.Today;
    }
}






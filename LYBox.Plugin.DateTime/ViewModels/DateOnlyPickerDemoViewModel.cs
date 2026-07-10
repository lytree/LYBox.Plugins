using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.DateTimeControls.Pages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LYBox.Plugin.DateTimeControls.ViewModels;

[NavigationItem("KeyDateOnlyPicker")]
[Menu("NAV_DateOnlyPicker", "KeyDateOnlyPicker", "NAV_DateTime")]
[ViewMap(typeof(DateOnlyPickerDemo))]
public partial class DateOnlyPickerDemoViewModel : ObservableObject
{
    [ObservableProperty] private System.DateOnly? _selectedDate;

    public DateOnlyPickerDemoViewModel()
    {
        SelectedDate = System.DateOnly.FromDateTime(System.DateTime.Today);
    }
}

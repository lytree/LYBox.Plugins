using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.DateTimeControls.Pages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LYBox.Plugin.DateTimeControls.ViewModels;

[NavigationItem("KeyDateOffsetPicker")]
[Menu("NAV_DateOffsetPicker", "KeyDateOffsetPicker", "NAV_DateTime")]
[ViewMap(typeof(DateOffsetPickerDemo))]
public partial class DateOffsetPickerDemoViewModel : ObservableObject
{
    [ObservableProperty] private System.DateTimeOffset? _selectedDate;

    public DateOffsetPickerDemoViewModel()
    {
        SelectedDate = System.DateTimeOffset.Now;
    }
}

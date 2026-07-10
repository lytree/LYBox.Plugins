using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.DateTimeControls.Pages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LYBox.Plugin.DateTimeControls.ViewModels;

[NavigationItem("KeyDateTimeOffsetPicker")]
[Menu("NAV_DateTimeOffsetPicker", "KeyDateTimeOffsetPicker", "NAV_DateTime")]
[ViewMap(typeof(DateTimeOffsetPickerDemo))]
public partial class DateTimeOffsetPickerDemoViewModel : ObservableObject
{
    [ObservableProperty] private System.DateTimeOffset? _selectedDateTime;

    public DateTimeOffsetPickerDemoViewModel()
    {
        SelectedDateTime = System.DateTimeOffset.Now;
    }
}

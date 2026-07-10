using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.DateTimeControls.Pages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LYBox.Plugin.DateTimeControls.ViewModels;

[NavigationItem("KeyTimeOnlyPicker")]
[Menu("NAV_TimeOnlyPicker", "KeyTimeOnlyPicker", "NAV_DateTime")]
[ViewMap(typeof(TimeOnlyPickerDemo))]
public partial class TimeOnlyPickerDemoViewModel : ObservableObject
{
    [ObservableProperty] private System.TimeOnly? _time;

    public TimeOnlyPickerDemoViewModel()
    {
        Time = new System.TimeOnly(12, 20, 0);
    }
}

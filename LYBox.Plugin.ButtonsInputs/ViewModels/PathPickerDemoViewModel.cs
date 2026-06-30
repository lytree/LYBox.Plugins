using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.ButtonsInputs.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LYBox.Plugin.ButtonsInputs.ViewModels;

[NavigationItem("PathPicker")]
[Menu("NAV_PathPicker", "PathPicker", "NAV_ButtonsInputs")]
[ViewMap(typeof(PathPickerDemo))]
public partial class PathPickerDemoViewModel : ViewModelBase
{
    [ObservableProperty] private string? _path;
    [ObservableProperty] private IReadOnlyList<string>? _paths;
    [ObservableProperty] private int _commandTriggerCount = 0;

    [RelayCommand]
    private void Selected(IReadOnlyList<string> paths)
    {
        CommandTriggerCount++;
    }
}






using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.LayoutDisplay.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avalonia.Plugin.LayoutDisplay.ViewModels;

[NavigationItem("KeyAvatar")]
[Menu("NAV_Avatar", "KeyAvatar", "NAV_LayoutDisplay", Status = "WIP")]
[ViewMap(typeof(AvatarDemo))]
public partial class AvatarDemoViewModel : ViewModelBase
{
    [ObservableProperty] private string _content = "AS";
    [ObservableProperty] private bool _canClick = true;

    [RelayCommand(CanExecute = nameof(CanClick))]
    private void Click()
    {
        Content = "BM";
    }
}






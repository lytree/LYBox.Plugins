using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.LayoutDisplay.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LYBox.Plugin.LayoutDisplay.ViewModels;

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






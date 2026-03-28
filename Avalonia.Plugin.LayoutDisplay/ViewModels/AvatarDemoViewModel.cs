using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avalonia.Plugin.LayoutDisplay.ViewModels;

[Menu("Avatar", MenuKeys.MenuKeyAvatar)]
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






using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.ButtonsInputs.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using Ursa.Common;

namespace Avalonia.Plugin.ButtonsInputs.ViewModels;

[NavigationItem("IconButton")]
[Menu("Icon Button", "IconButton", "ButtonsInputs")]
[ViewMap(typeof(IconButtonDemo))]
public partial class IconButtonDemoViewModel : ObservableObject
{
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoading2;
    [ObservableProperty] private Position _selectedPosition;
}






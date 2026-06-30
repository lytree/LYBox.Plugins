using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.ButtonsInputs.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using Ursa.Common;

namespace LYBox.Plugin.ButtonsInputs.ViewModels;

[NavigationItem("IconButton")]
[Menu("NAV_IconButton", "IconButton", "NAV_ButtonsInputs")]
[ViewMap(typeof(IconButtonDemo))]
public partial class IconButtonDemoViewModel : ObservableObject
{
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoading2;
    [ObservableProperty] private Position _selectedPosition;
}






using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.LayoutDisplay.Pages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LYBox.Plugin.LayoutDisplay.ViewModels;

[NavigationItem("KeyDisableContainer")]
[Menu("NAV_DisableContainer", "KeyDisableContainer", "NAV_LayoutDisplay")]
[ViewMap(typeof(DisableContainerDemo))]
public partial class DisableContainerDemoViewModel: ObservableObject
{
    
}






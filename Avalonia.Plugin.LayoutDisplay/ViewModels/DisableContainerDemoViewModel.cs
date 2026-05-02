using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.LayoutDisplay.Pages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.LayoutDisplay.ViewModels;

[NavigationItem("KeyDisableContainer")]
[Menu("Disable Container", "KeyDisableContainer", "Layout & Display")]
[ViewMap(typeof(DisableContainerDemo))]
public partial class DisableContainerDemoViewModel: ObservableObject
{
    
}






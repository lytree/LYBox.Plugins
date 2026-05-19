using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.LayoutDisplay.Pages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.LayoutDisplay.ViewModels;

[NavigationItem("KeyImageViewer")]
[Menu("NAV_ImageViewer", "KeyImageViewer", "NAV_LayoutDisplay")]
[ViewMap(typeof(ImageViewerDemo))]
public partial class ImageViewerDemoViewModel: ObservableObject
{
    
}






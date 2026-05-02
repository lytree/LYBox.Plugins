using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.LayoutDisplay.Pages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.LayoutDisplay.ViewModels;

[NavigationItem("KeyImageViewer")]
[Menu("ImageViewer", "KeyImageViewer", "Layout & Display")]
[ViewMap(typeof(ImageViewerDemo))]
public partial class ImageViewerDemoViewModel: ObservableObject
{
    
}






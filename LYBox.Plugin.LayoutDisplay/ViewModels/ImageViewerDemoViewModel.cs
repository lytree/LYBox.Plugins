using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.LayoutDisplay.Pages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LYBox.Plugin.LayoutDisplay.ViewModels;

[NavigationItem("KeyImageViewer")]
[Menu("NAV_ImageViewer", "KeyImageViewer", "NAV_LayoutDisplay")]
[ViewMap(typeof(ImageViewerDemo))]
public partial class ImageViewerDemoViewModel: ObservableObject
{
    
}






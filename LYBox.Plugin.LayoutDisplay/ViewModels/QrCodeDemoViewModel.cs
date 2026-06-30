using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.LayoutDisplay.Pages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LYBox.Plugin.LayoutDisplay.ViewModels;

[NavigationItem("KeyQrCode")]
[Menu("NAV_QrCode", "KeyQrCode", "NAV_LayoutDisplay", Status = "New")]
[ViewMap(typeof(QrCodeDemo))]
public partial class QrCodeDemoViewModel: ObservableObject
{
    
}






using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.LayoutDisplay.Pages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.LayoutDisplay.ViewModels;

[NavigationItem("KeyQrCode")]
[Menu("NAV_QrCode", "KeyQrCode", "NAV_LayoutDisplay", Status = "New")]
[ViewMap(typeof(QrCodeDemo))]
public partial class QrCodeDemoViewModel: ObservableObject
{
    
}






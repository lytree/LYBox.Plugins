using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.LayoutDisplay.Pages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.LayoutDisplay.ViewModels;

[NavigationItem("KeyQrCode")]
[Menu("Qr Code", "KeyQrCode", "Layout & Display", Status = "New")]
[ViewMap(typeof(QrCodeDemo))]
public partial class QrCodeDemoViewModel: ObservableObject
{
    
}






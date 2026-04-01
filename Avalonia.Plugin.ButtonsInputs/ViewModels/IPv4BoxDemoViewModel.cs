using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.ButtonsInputs.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Net;

namespace Avalonia.Plugin.ButtonsInputs.ViewModels;

[NavigationItem("IpBox")]
[Menu("IPv4Box", "IpBox", "ButtonsInputs")]
[ViewMap(typeof(IPv4BoxDemo))]
public partial class IPv4BoxDemoViewModel: ObservableObject
{
    [ObservableProperty] private IPAddress? _address;
    
    public IPv4BoxDemoViewModel()
    {
        Address = IPAddress.Parse("192.168.1.1");
    }
    public void ChangeAddress()
    {
        long l = Random.Shared.NextInt64(0x00000000FFFFFFFF);
        Address = new IPAddress(l);
    }
}






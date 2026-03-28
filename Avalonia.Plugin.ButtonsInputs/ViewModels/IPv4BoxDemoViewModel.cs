using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Net;

namespace Avalonia.Plugin.ButtonsInputs.ViewModels;

[Menu("IPv4Box", MenuKeys.MenuKeyIpBox)]
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






using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Input;

namespace Avalonia.Plugin.ButtonsInputs.ViewModels;

[Menu("KeyGestureInput", MenuKeys.MenuKeyKeyGestureInput)]
public class KeyGestureInputDemoViewModel: ObservableObject
{
    public List<Key> AcceptableKeys { get; set; } = new List<Key>()
    {
        Key.A, Key.B, Key.C,
    };
}






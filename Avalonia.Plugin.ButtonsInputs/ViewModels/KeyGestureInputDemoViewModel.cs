using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.ButtonsInputs.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Input;

namespace Avalonia.Plugin.ButtonsInputs.ViewModels;

[NavigationItem("KeyGestureInput")]
[Menu("KeyGestureInput", "KeyGestureInput", "ButtonsInputs")]
[ViewMap(typeof(KeyGestureInputDemo))]
public class KeyGestureInputDemoViewModel: ObservableObject
{
    public List<Key> AcceptableKeys { get; set; } = new List<Key>()
    {
        Key.A, Key.B, Key.C,
    };
}






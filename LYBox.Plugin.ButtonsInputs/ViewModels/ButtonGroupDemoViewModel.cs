using System.Collections.ObjectModel;
using System.Windows.Input;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.ButtonsInputs.Pages;
using CommunityToolkit.Mvvm.Input;
using Ursa.Controls;

namespace LYBox.Plugin.ButtonsInputs.ViewModels;

[NavigationItem("ButtonGroup")]
[Menu("NAV_ButtonGroup", "ButtonGroup", "NAV_ButtonsInputs")]
[ViewMap(typeof(ButtonGroupDemo))]
public class ButtonGroupDemoViewModel: ViewModelBase
{
    public ObservableCollection<ButtonItem> Items { get; set; } = new ()
    {
        new ButtonItem(){Name = "Ding" },
        new ButtonItem(){Name = "Otter" },
        new ButtonItem(){Name = "Husky" },
        new ButtonItem(){Name = "Mr. 17" },
        new ButtonItem(){Name = "Cass" },
    };
}

public class ButtonItem
{
    public string? Name { get; set; }
    public ICommand InvokeCommand { get; set; }

    public ButtonItem()
    {
        InvokeCommand = new AsyncRelayCommand(Invoke);
    }

    private async Task Invoke()
    {
        await OverlayMessageBox.ShowAsync("Hello " + Name);
    }
}






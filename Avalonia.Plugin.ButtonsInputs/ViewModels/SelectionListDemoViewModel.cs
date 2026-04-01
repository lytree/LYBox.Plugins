using System.Collections.ObjectModel;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.ButtonsInputs.Pages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.ButtonsInputs.ViewModels;

[NavigationItem("SelectionList")]
[Menu("Selection List", "SelectionList", "ButtonsInputs")]
[ViewMap(typeof(SelectionListDemo))]
public partial class SelectionListDemoViewModel: ObservableObject
{
    public ObservableCollection<string> Items { get; set; }
    [ObservableProperty] private string? _selectedItem;

    public SelectionListDemoViewModel()
    {
        Items = new ObservableCollection<string>()
        {
            "Ding", "Otter", "Husky", "Mr. 17", "Cass"
        };
        SelectedItem = Items[0];
    }

    public void Clear()
    {
        SelectedItem = null;
    }
}






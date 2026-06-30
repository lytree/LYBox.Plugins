using System.Collections.ObjectModel;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.ButtonsInputs.Pages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LYBox.Plugin.ButtonsInputs.ViewModels;

[NavigationItem("SelectionList")]
[Menu("NAV_SelectionList", "SelectionList", "NAV_ButtonsInputs")]
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






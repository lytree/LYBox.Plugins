using System.Collections.ObjectModel;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.ButtonsInputs.ViewModels;

[Menu("Selection List", MenuKeys.MenuKeySelectionList)]
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






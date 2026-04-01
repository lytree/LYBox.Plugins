using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.ButtonsInputs.Pages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.ButtonsInputs.ViewModels;

[NavigationItem("TreeComboBox")]
[Menu("TreeComboBox", "TreeComboBox", "ButtonsInputs")]
[ViewMap(typeof(TreeComboBoxDemo))]
public partial class TreeComboBoxDemoViewModel: ObservableObject
{
    [ObservableProperty] private TreeComboBoxItemViewModel? _selectedItem;
    public List<TreeComboBoxItemViewModel> Items { get; set; }

    public TreeComboBoxDemoViewModel()
    {
        Items = new List<TreeComboBoxItemViewModel>()
        {
            new TreeComboBoxItemViewModel()
            {
                ItemName = "Item 1",
                Children = new List<TreeComboBoxItemViewModel>()
                {
                    new TreeComboBoxItemViewModel()
                    {
                        ItemName = "Item 1-1 (Not selectable)",
                        IsSelectable = false,
                        Children = new List<TreeComboBoxItemViewModel>()
                        {
                            new TreeComboBoxItemViewModel()
                            {
                                ItemName = "Item 1-1-1"
                            },
                            new TreeComboBoxItemViewModel()
                            {
                                ItemName = "Item 1-1-2"
                            }
                        }
                    },
                    new TreeComboBoxItemViewModel()
                    {
                        ItemName = "Item 1-2"
                    }
                }
            },
            new TreeComboBoxItemViewModel()
            {
                ItemName = "Item 2",
                Children = new List<TreeComboBoxItemViewModel>()
                {
                    new TreeComboBoxItemViewModel()
                    {
                        ItemName = "Item 2-1  (Not selectable)",
                        IsSelectable = false,
                    },
                    new TreeComboBoxItemViewModel()
                    {
                        ItemName = "Item 2-2"
                    }
                }
            },
            new TreeComboBoxItemViewModel()
            {
                ItemName = "Item 3"
            },
        };
    }
}

public partial class TreeComboBoxItemViewModel : ObservableObject
{
    [ObservableProperty] private string? _itemName;
    [ObservableProperty] private bool _isSelectable = true;
    public List<TreeComboBoxItemViewModel> Children { get; set; } = new ();
}






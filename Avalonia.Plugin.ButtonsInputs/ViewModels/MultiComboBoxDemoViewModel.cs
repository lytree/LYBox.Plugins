using System.Collections.ObjectModel;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.ButtonsInputs.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace Avalonia.Plugin.ButtonsInputs.ViewModels;

[NavigationItem("MultiComboBox")]
[Menu("MultiComboBox", "MultiComboBox", "ButtonsInputs")]
[ViewMap(typeof(MultiComboBoxDemo))]
public class MultiComboBoxDemoViewModel: ObservableObject
{
    public ObservableCollection<string> Items { get; set; }
    
    public ObservableCollection<string> SelectedItems { get; set; }

    public ICommand SelectAllCommand => new RelayCommand(() =>
    {
        SelectedItems.Clear();
        foreach (var item in Items)
        {
            SelectedItems.Add(item);
        }
    });
    
    public ICommand ClearAllCommand => new RelayCommand(() =>
    {
        SelectedItems.Clear();
    });
    
    public ICommand InvertSelectionCommand => new RelayCommand(() =>
    {
        var selectedItems = new List<string>(SelectedItems);
        SelectedItems.Clear();
        foreach (var item in Items)
        {
            if (!selectedItems.Contains(item))
            {
                SelectedItems.Add(item);
            }
        }
    });
    
    public MultiComboBoxDemoViewModel()
    {
        Items = new ObservableCollection<string>()
        {
            "Item 1",
            "Item 2",
            "Item 3",
            "Item 4",
            "Item 5",
            "Item 6",
            "Item 7",
            "Item 8",
            "Illinois",
            "Indiana",
            "Iowa",
            "Kansas",
            "Kentucky",
            "Louisiana",
            "Maine",
            "Maryland",
            "Massachusetts",
            "Michigan",
            "Minnesota",
            "Mississippi",
            "Missouri",
            "Montana",
            "Nebraska",
            "Nevada",
            "New Hampshire",
            "New Jersey",
            "New Mexico",
            "New York",
            "North Carolina",
            "North Dakota",
            "Ohio",
            "Oklahoma",
            "Oregon",
            "Pennsylvania",
            "Rhode Island",
        };
        SelectedItems = new ObservableCollection<string>();
    }
}






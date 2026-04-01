using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.LayoutDisplay.ViewModels;


public class ScrollToButtonDemoViewModel: ObservableObject
{
    public ObservableCollection<string> Items { get; set; }

    public ScrollToButtonDemoViewModel()
    {
        Items = new ObservableCollection<string>(Enumerable.Range(0, 1000).Select(a => "Item " + a));
    }
}






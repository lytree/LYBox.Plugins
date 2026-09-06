using System.Collections.ObjectModel;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.LayoutDisplay.Pages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LYBox.Plugin.LayoutDisplay.ViewModels;

[NavigationItem("KeyScrollToButton")]
[Menu("NAV_ScrollToButton", "KeyScrollToButton", "NAV_LayoutDisplay")]
[ViewMap(typeof(ScrollToButtonDemo))]
public partial class ScrollToButtonDemoViewModel: ObservableObject
{
    public ObservableCollection<string> Items { get; set; }

    public ScrollToButtonDemoViewModel()
    {
        Items = new ObservableCollection<string>(Enumerable.Range(0, 1000).Select(a => "Item " + a));
    }
}






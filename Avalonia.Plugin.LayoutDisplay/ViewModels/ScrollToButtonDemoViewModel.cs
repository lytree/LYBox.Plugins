using System.Collections.ObjectModel;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.LayoutDisplay.Pages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.LayoutDisplay.ViewModels;

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






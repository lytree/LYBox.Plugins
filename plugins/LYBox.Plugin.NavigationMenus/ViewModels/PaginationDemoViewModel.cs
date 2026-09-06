using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.NavigationMenus.Pages;
using System.Diagnostics;
using System.Windows.Input;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.Input;

namespace LYBox.Plugin.NavigationMenus.ViewModels;

[NavigationItem("Pagination")]
[Menu("NAV_Pagination", "Pagination", "NAV_NavigationMenus")]
[ViewMap(typeof(PaginationDemo))]
public partial class PaginationDemoViewModel : ViewModelBase
{
    public AvaloniaList<int> PageSizes { get; set; } = new() { 10, 20, 50, 100 };

    public ICommand LoadPageCommand { get; }
    public PaginationDemoViewModel()
    {
        this.LoadPageCommand = new RelayCommand<int?>(LoadPage);
    }

    private void LoadPage(int? pageIndex)
    {
        Debug.WriteLine($"Loading page {pageIndex}");
    }
}






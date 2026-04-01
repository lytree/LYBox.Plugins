using Avalonia.Plugin.Shared;
using Avalonia.Plugin.NavigationMenus.ViewModels;
using Avalonia.Plugin.Shared.ViewModels;

namespace Avalonia.Plugin.NavigationMenus;

public  partial  class NavigationMenusPlugin : IPluginMetadata
{
    public string Name => "Navigation & Menus Plugin";
    public string Version => "1.0.0";

    public string Author => throw new NotImplementedException();

    public string Description => throw new NotImplementedException();

    public IEnumerable<string> Dependencies => throw new NotImplementedException();

    public string PluginId => throw new NotImplementedException();

    public void Initialize()
    {
        // 插件初始化逻辑
    }

    public Dictionary<string, ViewModelFactory> GetNavigationItems()
    {
        var navigationItems = new Dictionary<string, ViewModelFactory>
        {
            { "Anchor", () => new AnchorDemoViewModel() },
            { "Breadcrumb", () => new BreadcrumbDemoViewModel() },
            { "NavMenu", () => new NavMenuDemoViewModel() },
            { "Pagination", () => new PaginationDemoViewModel() },
            { "ToolBar", () => new ToolBarDemoViewModel() },
        };

        return navigationItems;
    }

    public IEnumerable<(string? ParentKey, MenuItemViewModel MenuItem)> GetMenuItems()
    {
        var menuItems = new List<(string? ParentKey, MenuItemViewModel MenuItem)>();

        var navigationAndMenus = new MenuItemViewModel
        {
            MenuHeader = "Navigation & Menus",
            Children = new()
            {
                new() { MenuHeader = "Anchor", Key = "Anchor" },
                new() { MenuHeader = "Breadcrumb", Key = "Breadcrumb" },
                new() { MenuHeader = "Nav Menu", Key = "NavMenu", Status = "Updated" },
                new() { MenuHeader = "Pagination", Key = "Pagination" },
                new() { MenuHeader = "ToolBar", Key = "ToolBar" },
            }
        };
        menuItems.Add((null, navigationAndMenus));

        return menuItems;
    }


}




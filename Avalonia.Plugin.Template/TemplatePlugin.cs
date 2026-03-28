using System.Collections.Generic;


namespace Avalonia.Plugin.Template;

public class TemplatePlugin : IPlugin
{
    public string Name => "Template Plugin";
    public string Version => "1.0.0";

    public void Initialize()
    {
        // 插件初始化逻辑
    }

    public Dictionary<string, ViewModelFactory> GetNavigationItems()
    {
        var navigationItems = new Dictionary<string, ViewModelFactory>();
        // 添加导航项
        return navigationItems;
    }

    public IEnumerable<(string? ParentKey, MenuItemViewModel MenuItem)> GetMenuItems()
    {
        var menuItems = new List<(string? ParentKey, MenuItemViewModel MenuItem)>();
        // 添加菜单项
        return menuItems;
    }
}




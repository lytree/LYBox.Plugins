using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.ViewModels;
using System.Collections.Generic;


namespace Avalonia.Plugin.Template;

public class TemplatePlugin : IPlugin
{
    public string Name => "Template Plugin";
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

    IEnumerable<(string? ParentKey, MenuItemViewModel MenuItem)> IPlugin.GetMenuItems()
    {
        throw new NotImplementedException();
    }
}




using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;

namespace Avalonia.Plugin.NavigationMenus;

[GenerateMetadata]
public partial class NavigationMenusPlugin : IPluginMetadata
{
    public string Name => "Navigation & Menus Plugin";
    public string Version => "1.0.0";
    public string Author => "AvaloniaTemplate";
    public string Description => "Navigation and menu controls demo plugin.";
    public IEnumerable<string> Dependencies => [];
    public string PluginId => "Avalonia.Plugin.NavigationMenus";

    public void Initialize()
    {
    }
}

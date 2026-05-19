#nullable enable
namespace Avalonia.Plugin.NavigationMenus.Resources;

public static class Strings
{
    private static global::System.Resources.ResourceManager? _resourceManager;

    public static global::System.Resources.ResourceManager ResourceManager
    {
        get
        {
            if (_resourceManager is null)
            {
                _resourceManager = new global::System.Resources.ResourceManager(
                    "Avalonia.Plugin.NavigationMenus.Resources.Strings",
                    typeof(Strings).Assembly);
            }
            return _resourceManager;
        }
    }

    private static global::System.Globalization.CultureInfo? _culture;

    public static global::System.Globalization.CultureInfo Culture
    {
        get => _culture ?? global::System.Globalization.CultureInfo.CurrentUICulture;
        set => _culture = value;
    }

    public static string NAV_NavigationMenus => ResourceManager.GetString(nameof(NAV_NavigationMenus), Culture)!;
    public static string NAV_NavMenu => ResourceManager.GetString(nameof(NAV_NavMenu), Culture)!;
    public static string NAV_Breadcrumb => ResourceManager.GetString(nameof(NAV_Breadcrumb), Culture)!;
    public static string NAV_Pagination => ResourceManager.GetString(nameof(NAV_Pagination), Culture)!;
    public static string NAV_Anchor => ResourceManager.GetString(nameof(NAV_Anchor), Culture)!;
    public static string NAV_ToolBar => ResourceManager.GetString(nameof(NAV_ToolBar), Culture)!;
}

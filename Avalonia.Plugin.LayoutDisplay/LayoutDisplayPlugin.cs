using System.Collections.Generic;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.LayoutDisplay.ViewModels;
using Avalonia.Plugin.Shared.ViewModels;
using System.Reflection;

namespace Avalonia.Plugin.LayoutDisplay;

public class LayoutDisplayPlugin : IPlugin
{
    public string Name => "Layout & Display Plugin";
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
            { "KeyAspectRatioLayout", () => new AspectRatioLayoutDemoViewModel() },
            { "KeyAvatar", () => new AvatarDemoViewModel() },
            { "KeyBadge", () => new BadgeDemoViewModel() },
            { "KeyBanner", () => new BannerDemoViewModel() },
            { "KeyDescriptions", () => new DescriptionsDemoViewModel() },
            { "KeyDisableContainer", () => new DisableContainerDemoViewModel() },
            { "KeyDivider", () => new DividerDemoViewModel() },
            { "KeyDualBadge", () => new DualBadgeDemoViewModel() },
            { "KeyImageViewer", () => new ImageViewerDemoViewModel() },
            { "KeyElasticWrapPanel", () => new ElasticWrapPanelDemoViewModel() },
            { "KeyMarquee", () => new MarqueeDemoViewModel() },
            { "KeyNumberDisplayer", () => new NumberDisplayerDemoViewModel() },
            { "KeyQrCode", () => new QrCodeDemoViewModel() },
            { "KeyScrollToButton", () => new ScrollToButtonDemoViewModel() },
            { "KeyTimeline", () => new TimelineDemoViewModel() },
            { "KeyTwoTonePathIcon", () => new TwoTonePathIconDemoViewModel() }
        };

        return navigationItems;
    }

    public IEnumerable<(string? ParentKey, MenuItemViewModel MenuItem)> GetMenuItems()
    {
        var menuItems = new List<(string? ParentKey, MenuItemViewModel MenuItem)>();

        var layoutAndDisplay = new MenuItemViewModel
        {
            MenuHeader = "Layout & Display",
            Children = new()
            {
                new() { MenuHeader = "AspectRatioLayout", Key = "KeyAspectRatioLayout" },
                new() { MenuHeader = "Avatar", Key = "KeyAvatar", Status = "WIP" },
                new() { MenuHeader = "Badge", Key = "KeyBadge" },
                new() { MenuHeader = "Banner", Key = "KeyBanner" },
                new() { MenuHeader = "Descriptions", Key = "KeyDescriptions", Status = "New" },
                new() { MenuHeader = "Disable Container", Key = "KeyDisableContainer" },
                new() { MenuHeader = "Divider", Key = "KeyDivider" },
                new() { MenuHeader = "DualBadge", Key = "KeyDualBadge" },
                new() { MenuHeader = "ImageViewer", Key = "KeyImageViewer" },
                new() { MenuHeader = "ElasticWrapPanel", Key = "KeyElasticWrapPanel" },
                new() { MenuHeader = "Marquee", Key = "KeyMarquee" },
                new() { MenuHeader = "Number Displayer", Key = "KeyNumberDisplayer", Status = "Updated" },
                new() { MenuHeader = "Qr Code", Key = "KeyQrCode", Status = "New" },
                new() { MenuHeader = "Scroll To", Key = "KeyScrollToButton" },
                new() { MenuHeader = "Timeline", Key = "KeyTimeline" },
                new() { MenuHeader = "TwoTonePathIcon", Key = "KeyTwoTonePathIcon" }
            }
        };
        menuItems.Add((layoutAndDisplay.MenuHeader, layoutAndDisplay));

        return menuItems;
    }

    public Assembly GetAssembly()
    {
        throw new NotImplementedException();
    }
}




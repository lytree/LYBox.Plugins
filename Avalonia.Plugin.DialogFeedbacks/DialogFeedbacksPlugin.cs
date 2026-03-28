using System.Collections.Generic;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.DialogFeedbacks.ViewModels;
using Avalonia.Plugin.Shared.ViewModels;
using System.Reflection;

namespace Avalonia.Plugin.DialogFeedbacks;

public class DialogFeedbacksPlugin : IPlugin
{
    public string Name => "Dialog & Feedbacks Plugin";
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
            { "KeyDialog", () => new DialogDemoViewModel() },
            { "KeyDrawer", () => new DrawerDemoViewModel() },
            { "KeyLoading", () => new LoadingDemoViewModel() },
            { "KeyMessageBox", () => new MessageBoxDemoViewModel() },
            { "KeyNotification", () => new NotificationDemoViewModel() },
            { "KeyPopConfirm", () => new PopConfirmDemoViewModel() },
            { "KeyToast", () => new ToastDemoViewModel() },
            { "KeySkeleton", () => new SkeletonDemoViewModel() },
        };

        return navigationItems;
    }

    public IEnumerable<(string? ParentKey, MenuItemViewModel MenuItem)> GetMenuItems()
    {
        var menuItems = new List<(string? ParentKey, MenuItemViewModel MenuItem)>();

        var dialogAndFeedbacks = new MenuItemViewModel
        {
            MenuHeader = "Dialog & Feedbacks",
            Children = new()
            {
                new() { MenuHeader = "Dialog", Key = "KeyDialog" },
                new() { MenuHeader = "Drawer", Key = "KeyDrawer" },
                new() { MenuHeader = "Loading", Key = "KeyLoading" },
                new() { MenuHeader = "Message Box", Key = "KeyMessageBox" },
                new() { MenuHeader = "Notification", Key = "KeyNotification" },
                new() { MenuHeader = "PopConfirm", Key = "KeyPopConfirm" },
                new() { MenuHeader = "Toast", Key = "KeyToast" },
                new() { MenuHeader = "Skeleton", Key = "KeySkeleton" },
            }
        };
        menuItems.Add((null, dialogAndFeedbacks));

        return menuItems;
    }

    public Assembly GetAssembly()
    {
        throw new NotImplementedException();
    }
}




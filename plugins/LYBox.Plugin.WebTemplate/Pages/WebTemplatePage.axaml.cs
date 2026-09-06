using Avalonia.Controls;
using Avalonia.Threading;
using LYBox.Plugin.Shared.Rpc;
using LYBox.Plugin.Shared.Web;
using LYBox.Plugin.WebTemplate.ViewModels;

namespace LYBox.Plugin.WebTemplate.Pages;

public partial class WebTemplatePage : UserControl
{
    private DispatcherTimer? _pushTimer;
    private int _tickCount;

    public WebTemplatePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _pushTimer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Normal, OnPushTick);
        _pushTimer.Start();
    }

    private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _pushTimer?.Stop();
        _pushTimer = null;
    }

    private async void OnPushTick(object? sender, EventArgs e)
    {
        // ToggleSwitch 关闭时暂停推送
        if (DataContext is WebTemplatePageViewModel { IsPushing: false }) return;

        // WebPluginView.RpcHost 在 NavigationCompleted + ipc.js 注入后就绪
        if (WebView?.RpcHost is not { } host) return;

        _tickCount++;
        try
        {
            await host.EmitEventAsync("tick", new
            {
                count = _tickCount,
                time = DateTime.Now.ToString("HH:mm:ss"),
                message = $"这是 C# 第 {_tickCount} 次主动推送"
            });
        }
        catch
        {
            // 页面未就绪或已销毁，忽略
        }
    }
}

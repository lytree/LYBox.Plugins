using Avalonia.Controls;
using LYBox.Plugin.Zitie.Rpc;

namespace LYBox.Plugin.Zitie.Pages;

/// <summary>
/// 字帖生成器承载页。
/// </summary>
/// <remarks>
/// 页面本身零业务逻辑，唯一的职责是把窗口上下文交给 <see cref="ZitieNativeHost"/>：
/// 原生保存 / 打开对话框需要 <c>StorageProvider</c>，而它只能从活动的 TopLevel 取得，
/// <c>[RpcCommand]</c> 类经 DI 实例化拿不到视觉树，故在此注册。
/// </remarks>
public partial class ZitiePage : UserControl
{
    public ZitiePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ZitieNativeHost.Attach(TopLevel.GetTopLevel(this));

    private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ZitieNativeHost.Detach();
}

using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Messages;
using LYBox.Plugin.TDLSharp.Resources;
using LYBox.Plugin.TDLSharp.ViewModels;

namespace LYBox.Plugin.TDLSharp.Services;

/// <summary>
/// TDLSharp 插件的运行期状态展示中枢。
///
/// <para>职责：</para>
/// <list type="bullet">
///   <item>在宿主 Setting 页面中暴露的卡片（状态/初始化按钮/扫码按钮/退出按钮）上同步 TDLib 当前授权状态文案；</item>
///   <item>订阅宿主派发的 <see cref="SettingActionInvokedMessage"/>，执行"初始化/打开登录弹框/退出登录"等动作。</item>
/// </list>
/// <para>由 <see cref="TDLSharpPlugin"/> 在 <c>RegisterAsync</c> 中实例化并由 DI 容器持有。</para>
/// </summary>
public sealed class TdlPluginStatusController : IDisposable
{
    private readonly TdlClientManager _clientManager;
    private bool _disposed;

    /// <summary>宿主 Setting 中"执行初始化"动作的 Key（与 <see cref="LYBox.Plugin.Shared.Models.SettingItem.Key"/> 对应）。</summary>
    public const string ActionInitialize = "TDL.Action.Initialize";
    /// <summary>宿主 Setting 中"扫码登录"动作的 Key。</summary>
    public const string ActionQrLogin = "TDL.Action.QrLogin";
    /// <summary>宿主 Setting 中"退出登录"动作的 Key。</summary>
    public const string ActionLogout = "TDL.Action.Logout";
    /// <summary>宿主 Setting 中"当前状态"只读条目的 Key（同步显示 TDLib AuthState 文案）。</summary>
    public const string StatusAuthState = "TDL.Status.AuthState";

    public TdlPluginStatusController(TdlClientManager clientManager)
    {
        _clientManager = clientManager;
        _clientManager.AuthStateChanged += OnAuthStateChanged;
        WeakReferenceMessenger.Default.Register<TdlPluginStatusController, SettingActionInvokedMessage>(this, OnActionInvoked);
        DispatchOnUi(SyncStatusText);
    }

    private void OnAuthStateChanged()
    {
        DispatchOnUi(SyncStatusText);
    }

    private void SyncStatusText()
    {
        var text = BuildStatusText();
        WeakReferenceMessenger.Default.Send(new SettingStatusChangedMessage(StatusAuthState, text));
    }

    private string BuildStatusText()
    {
        if (!_clientManager.HasTdlRoot)
            return Strings.Get("LOGIN_TdlRootNotSet");

        var state = _clientManager.AuthState;
        if (state == AuthStateCode.Unknown)
            return Strings.Get("LOGIN_StatusIdle");

        return state switch
        {
            AuthStateCode.Ready => Strings.Get("LOGIN_StatusReady"),
            AuthStateCode.WaitPhoneNumber => Strings.Get("LOGIN_StatusWaitPhone"),
            AuthStateCode.WaitCode => Strings.Get("LOGIN_StatusWaitCode"),
            AuthStateCode.WaitPassword => Strings.Get("LOGIN_StatusWaitPassword"),
            AuthStateCode.WaitRegistration => Strings.Get("LOGIN_StatusWaitRegistration"),
            AuthStateCode.WaitOtherDeviceConfirmation => Strings.Get("LOGIN_StatusWaitOtherDevice"),
            _ => state.ToString()
        };
    }

    private void OnActionInvoked(TdlPluginStatusController recipient, SettingActionInvokedMessage message)
    {
        DispatchOnUi(async () =>
        {
            try
            {
                switch (message.ActionId)
                {
                    case ActionInitialize:
                        await DoInitializeAsync();
                        break;
                    case ActionQrLogin:
                        await DoShowQrLoginAsync();
                        break;
                    case ActionLogout:
                        await DoLogoutAsync();
                        break;
                }
            }
            catch
            {
                // 静默失败：状态文本会自动反映下一轮 AuthState 变更。
            }
        });
    }

    private async Task DoInitializeAsync()
    {
        if (!_clientManager.HasTdlRoot)
        {
            await LoginDialogService.ShowLoginDialogAsync(LoginMethod.QrCode);
            return;
        }

        try
        {
            await _clientManager.EnsureInitializedAsync();
            await _clientManager.WaitReadyAsync();
            // 初始化完成后若仍需要登录，主动弹二维码登录。
            if (_clientManager.AuthState.NeedsLogin())
            {
                await LoginDialogService.ShowLoginDialogAsync(LoginMethod.QrCode);
            }
        }
        catch
        {
            // ignored; UI 通过状态变更体现
        }
    }

    private async Task DoShowQrLoginAsync()
    {
        if (!_clientManager.HasTdlRoot)
        {
            await LoginDialogService.ShowLoginDialogAsync(LoginMethod.QrCode);
            return;
        }

        if (_clientManager.AuthState == AuthStateCode.Unknown)
        {
            await _clientManager.EnsureInitializedAsync();
            await _clientManager.WaitReadyAsync();
        }

        await LoginDialogService.ShowLoginDialogAsync(LoginMethod.QrCode);
    }

    private async Task DoLogoutAsync()
    {
        await _clientManager.LogoutAsync();
    }

    private static void DispatchOnUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _clientManager.AuthStateChanged -= OnAuthStateChanged;
        // 取消该实例在 WeakReferenceMessenger 上的所有订阅。
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }
}

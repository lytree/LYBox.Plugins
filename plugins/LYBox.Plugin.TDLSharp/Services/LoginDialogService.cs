using LYBox.Plugin.TDLSharp.Resources;
using LYBox.Plugin.TDLSharp.ViewModels;
using Ursa.Controls;

namespace LYBox.Plugin.TDLSharp.Services;

/// <summary>
/// Shows the login dialog as an overlay. Returns true if the user authenticated successfully.
/// </summary>
public static class LoginDialogService
{
    /// <summary>
    /// Show the login dialog. Returns true if authentication succeeded, false otherwise.
    /// </summary>
    /// <param name="preferredMethod">Optional preferred login method. When provided, the dialog opens
    /// with the specified tab selected (e.g. <see cref="LoginMethod.QrCode"/> when triggered from a page-level QR button).</param>
    public static async Task<bool> ShowLoginDialogAsync(LoginMethod? preferredMethod = null)
    {
        var vm = new LoginViewModel();
        if (preferredMethod.HasValue)
        {
            vm.SelectedLoginMethod = preferredMethod.Value;
        }
        var options = new OverlayDialogOptions
        {
            Title = Strings.Get("LOGIN_Title"),
            CanResize = false,
            CanLightDismiss = false,
            IsCloseButtonVisible = true,
            HorizontalAnchor = HorizontalPosition.Center,
            VerticalAnchor = VerticalPosition.Center,
        };
        var result = await OverlayDialog.ShowCustomAsync<Controls.LoginDialog, LoginViewModel, bool>(vm, options: options);
        return result is true;
    }
}

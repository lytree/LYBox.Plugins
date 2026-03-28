using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.Input;
using Ursa.Controls;

namespace Avalonia.Plugin.DialogFeedbacks.ViewModels;

[Menu("PopConfirm", MenuKeys.MenuKeyPopConfirm)]
public class PopConfirmDemoViewModel : ObservableObject
{
    public PopConfirmDemoViewModel()
    {
        AsyncConfirmCommand = new AsyncRelayCommand(OnConfirmAsync);
        AsyncCancelCommand = new RelayCommand(OnCancelAsync);
        ConfirmCommand = new RelayCommand(OnConfirm);
        CancelCommand = new RelayCommand(OnCancel);
    }

    internal WindowToastManager? ToastManager { get; set; }

    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }

    public ICommand AsyncConfirmCommand { get; }
    public ICommand AsyncCancelCommand { get; }

    private void OnCancel()
    {
        ToastManager?.Show(new Toast("Canceled"), NotificationType.Error, classes: ["Light"]);
    }

    private void OnConfirm()
    {
        ToastManager?.Show(new Toast("Confirmed"), NotificationType.Success, classes: ["Light"]);
    }

    private async Task OnConfirmAsync()
    {
        await Task.Delay(3000);
        ToastManager?.Show(new Toast("Async Confirmed"), NotificationType.Success, classes: ["Light"]);
    }

    private void OnCancelAsync()
    {
        ToastManager?.Show(new Toast("Async Canceled"), NotificationType.Error, classes: ["Light"]);
    }
}






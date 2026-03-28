using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Ursa.Controls;

namespace Avalonia.Plugin.ButtonsInputs.ViewModels;


public partial class PinCodeDemoViewModel: ObservableObject
{
    public ICommand CompleteCommand { get; set; }
    [ObservableProperty] private List<Exception>? _error;

    public PinCodeDemoViewModel()
    {
        CompleteCommand = new AsyncRelayCommand<IList<string>>(OnComplete);
        Error = [new Exception("Invalid verification code")];
    }

    private async Task OnComplete(IList<string>? obj)
    {
        if (obj is null) return;
        var code = string.Join("", obj);
        await MessageBox.ShowOverlayAsync(code);
    }
}






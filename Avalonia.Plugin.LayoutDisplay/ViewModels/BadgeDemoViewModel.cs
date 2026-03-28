using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avalonia.Plugin.LayoutDisplay.ViewModels;

[Menu("Badge", MenuKeys.MenuKeyBadge)]
public partial class BadgeDemoViewModel: ViewModelBase
{
    [ObservableProperty] private string? _text = null;

    public BadgeDemoViewModel()
    {
        
    }

    [RelayCommand]
    public void ChangeText()
    {
        if (Text == null)
        {
            Text = DateTime.Now.ToShortDateString();
        }
        else
        {
            Text = null;
        }
    }
}






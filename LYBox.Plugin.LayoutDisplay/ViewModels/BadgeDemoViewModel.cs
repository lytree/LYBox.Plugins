using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.LayoutDisplay.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LYBox.Plugin.LayoutDisplay.ViewModels;

[NavigationItem("KeyBadge")]
[Menu("NAV_Badge", "KeyBadge", "NAV_LayoutDisplay")]
[ViewMap(typeof(BadgeDemo))]
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






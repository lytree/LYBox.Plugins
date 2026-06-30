using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LYBox.Plugin.Template.ViewModels;

[NavigationItem("TemplateDemo")]
[Menu("NAV_TemplateDemo", "TemplateDemo", ParentKey = null, Status = "New", Order = 999)]
[ViewMap(typeof(Pages.TemplatePage))]
public partial class TemplatePageViewModel : ViewModelBase
{
    [ObservableProperty] private string _welcomeMessage = "Hello from Template Plugin!";
    [ObservableProperty] private int _clickCount;

    [RelayCommand]
    private void IncrementCount()
    {
        ClickCount++;
        WelcomeMessage = $"You clicked {ClickCount} time(s)!";
    }

    [RelayCommand]
    private void ResetCount()
    {
        ClickCount = 0;
        WelcomeMessage = "Hello from Template Plugin!";
    }
}

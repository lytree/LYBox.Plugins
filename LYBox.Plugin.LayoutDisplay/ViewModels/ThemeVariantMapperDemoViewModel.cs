using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.LayoutDisplay.Pages;

namespace LYBox.Plugin.LayoutDisplay.ViewModels;

[NavigationItem("KeyThemeVariantMapper")]
[Menu("NAV_ThemeVariantMapper", "KeyThemeVariantMapper", "NAV_LayoutDisplay")]
[ViewMap(typeof(ThemeVariantMapperDemo))]
public class ThemeVariantMapperDemoViewModel : ViewModelBase
{
}

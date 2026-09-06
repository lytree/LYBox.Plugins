using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.LayoutDisplay.Pages;

namespace LYBox.Plugin.LayoutDisplay.ViewModels;

[NavigationItem("KeyGroupBox")]
[Menu("NAV_GroupBox", "KeyGroupBox", "NAV_LayoutDisplay")]
[ViewMap(typeof(GroupBoxDemo))]
public class GroupBoxDemoViewModel : ViewModelBase
{
}

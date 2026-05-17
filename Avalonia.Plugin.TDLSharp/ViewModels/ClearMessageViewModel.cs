using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Services;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_ClearMessage")]
[Menu("清理消息", "TDL_ClearMessage", ParentKey = "TDL", Order = 2)]
[ViewMap(typeof(Pages.ClearMessagePage))]
public partial class ClearMessageViewModel : TdlViewModelBase
{
    public override ScriptDescriptor Script => ScriptDefinitions.All[1];
}

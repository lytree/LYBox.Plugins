using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Services;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_DeepCopy")]
[Menu("深度Copy转发", "TDL_DeepCopy", ParentKey = "TDL", Order = 3)]
[ViewMap(typeof(Pages.DeepCopyPage))]
public partial class DeepCopyViewModel : TdlViewModelBase
{
    public override ScriptDescriptor Script => ScriptDefinitions.All[2];
}

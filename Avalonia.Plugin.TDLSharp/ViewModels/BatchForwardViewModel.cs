using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Services;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_BatchForward")]
[Menu("批量深度转发", "TDL_BatchForward", ParentKey = "TDL", Order = 1)]
[ViewMap(typeof(Pages.BatchForwardPage))]
public partial class BatchForwardViewModel : TdlViewModelBase
{
    public override ScriptDescriptor Script => ScriptDefinitions.All[0];
}

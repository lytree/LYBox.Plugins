using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Services;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_MessageExport")]
[Menu("消息导出", "TDL_MessageExport", ParentKey = "TDL", Order = 5)]
[ViewMap(typeof(Pages.MessageExportPage))]
public partial class MessageExportViewModel : TdlViewModelBase
{
    public override ScriptDescriptor Script => ScriptDefinitions.All[4];
}

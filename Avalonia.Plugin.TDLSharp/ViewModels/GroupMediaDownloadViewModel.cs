using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.Services;

namespace Avalonia.Plugin.TDLSharp.ViewModels;

[NavigationItem("TDL_GroupMediaDownload")]
[Menu("群组媒体下载", "TDL_GroupMediaDownload", ParentKey = "TDL", Order = 4)]
[ViewMap(typeof(Pages.GroupMediaDownloadPage))]
public partial class GroupMediaDownloadViewModel : TdlViewModelBase
{
    public override ScriptDescriptor Script => ScriptDefinitions.All[3];
}

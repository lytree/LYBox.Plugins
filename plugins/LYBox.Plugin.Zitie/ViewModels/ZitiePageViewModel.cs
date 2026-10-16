using CommunityToolkit.Mvvm.ComponentModel;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;

namespace LYBox.Plugin.Zitie.ViewModels;

/// <summary>
/// 字帖生成器页面 ViewModel。
/// </summary>
/// <remarks>
/// 页面主体完全由 WebView 内的 Vue 应用承载，此处只持有 WebPluginView 路由所需的 PluginId
/// 与加载状态。<c>ZitiePage.axaml</c> 不加顶部控制栏——字帖应用自带完整工具栏，
/// 宿主侧再套一层只会浪费纵向空间。
/// </remarks>
[NavigationItem("Zitie")]
[Menu("字帖生成器", "Zitie", ParentKey = null, Status = "New", Order = 996)]
[ViewMap(typeof(Pages.ZitiePage))]
public partial class ZitiePageViewModel : ViewModelBase
{
    /// <summary>
    /// 与 <c>ZitiePlugin</c> 的 PluginId 一致，用于 Kestrel 路由前缀（/{PluginId}/...）
    /// 与 SSE 通道（/sse/{PluginId}）。改动须同步 csproj 与 ui/vite.config.ts。
    /// </summary>
    [ObservableProperty]
    private string _pluginId = "5e3f7a21-9c48-4d6b-b1f2-7a8e3c4d5b60";
}

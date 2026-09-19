using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LYBox.Plugin.ViteSample.ViewModels;

[NavigationItem("ViteSample")]
[Menu("Vite Sample", "ViteSample", ParentKey = null, Status = "Dev", Order = 997)]
[ViewMap(typeof(Pages.ViteSamplePage))]
public partial class ViteSamplePageViewModel : ViewModelBase
{
    /// <summary>
    /// 与 <see cref="ViteSamplePlugin"/> 的 PluginId 一致，
    /// 用于 Kestrel 路由前缀（/{PluginId}/...）+ SSE 通道（/sse/{PluginId}）。
    /// </summary>
    [ObservableProperty]
    private string _pluginId = "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d";

    /// <summary>SSE 推送开关：true = 每 2 秒推一次 tick 事件到前端。</summary>
    [ObservableProperty]
    private bool _isPushing = true;

    [ObservableProperty]
    private string _statusMessage = "WebView 加载中...";
}
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LYBox.Plugin.WebTemplate.ViewModels;

[NavigationItem("WebTemplateDemo")]
[Menu("Web Template Demo", "WebTemplateDemo", ParentKey = null, Status = "New", Order = 998)]
[ViewMap(typeof(Pages.WebTemplatePage))]
public partial class WebTemplatePageViewModel : ViewModelBase
{
    /// <summary>PluginId 与 WebTemplatePlugin.PluginId 一致，用于 Kestrel 路由 + SSE 通道。</summary>
    [ObservableProperty]
    private string _pluginId = "8a7b6c5d-4e3f-4a2b-9c1d-0e8f7a6b5c4d";

    [ObservableProperty]
    private bool _isPushing = true;

    [ObservableProperty]
    private string _statusMessage = "WebView 加载中...";
}

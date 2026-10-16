using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.Shared.Web;

namespace LYBox.Plugin.Zitie;

/// <summary>
/// 字帖生成器 Web 插件入口。
/// 源生成器 [GenerateMetadata] 从 csproj 的 &lt;PluginKind&gt;Web&lt;/PluginKind&gt; 自动实现
/// IWebPlugin.Web 描述符，宿主据此统一注册 wwwroot（无需手动调用 MapPluginRoot）。
/// </summary>
[GenerateMetadata]
public partial class ZitiePlugin : IWebPlugin
{
}

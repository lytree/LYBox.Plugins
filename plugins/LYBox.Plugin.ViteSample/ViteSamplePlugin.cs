using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.Shared.Web;

namespace LYBox.Plugin.ViteSample;

/// <summary>
/// Vite + TypeScript 端到端示例插件。
/// 源生成器 [GenerateMetadata] 从 csproj 的 <PluginKind>Web</PluginKind> 自动实现 IWebPlugin.Web 描述符，
/// 宿主据此统一注册 wwwroot（无需手动调用 MapPluginRoot）。
/// </summary>
[GenerateMetadata]
public partial class ViteSamplePlugin : IWebPlugin
{
}
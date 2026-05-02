using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;

namespace Avalonia.Plugin.Template;

[GenerateMetadata]
public partial class TemplatePlugin : IPluginMetadata
{
    public string Name => "Template Plugin";
    public string Version => "1.0.0";
    public string Author => "AvaloniaTemplate";
    public string Description => "A minimal template plugin demonstrating the plugin system with ViewModel-View binding and menu integration.";
    public IEnumerable<string> Dependencies => [];
    public string PluginId => "b5eab285-8673-4991-a45a-b43bee2cb840";

    public void Initialize()
    {
    }
}

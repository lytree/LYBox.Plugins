using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;

namespace Avalonia.Plugin.LayoutDisplay;

[GenerateMetadata]
public partial class LayoutDisplayPlugin : IPluginMetadata
{
    public string Name => "Layout & Display Plugin";
    public string Version => "1.0.0";
    public string Author => "AvaloniaTemplate";
    public string Description => "Layout and display controls demo plugin.";
    public IEnumerable<string> Dependencies => [];
    public string PluginId => "Avalonia.Plugin.LayoutDisplay";

    public void Initialize()
    {
    }
}

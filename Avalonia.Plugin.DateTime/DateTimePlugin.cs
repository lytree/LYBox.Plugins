using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;

namespace Avalonia.Plugin.DateTimeControls;

[GenerateMetadata]
public partial class DateTimePlugin : IPluginMetadata
{
    public string Name => "Date & Time Plugin";
    public string Version => "1.0.0";
    public string Author => "AvaloniaTemplate";
    public string Description => "Date and time controls demo plugin.";
    public IEnumerable<string> Dependencies => [];
    public string PluginId => "Avalonia.Plugin.DateTime";

    public void Initialize()
    {
    }
}

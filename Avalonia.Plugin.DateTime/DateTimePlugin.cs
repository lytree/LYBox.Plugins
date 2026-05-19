using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.Shared.Services;
using Avalonia.Plugin.DateTimeControls.Resources;

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
        if (ServiceLocator.TryGetService<ILocalizationService>(out var loc) && loc is not null)
            loc.RegisterResourceManager(Strings.ResourceManager);
    }
}

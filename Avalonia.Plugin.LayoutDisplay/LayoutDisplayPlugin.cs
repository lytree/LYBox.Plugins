using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.Shared.Services;
using Avalonia.Plugin.LayoutDisplay.Resources;

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
        if (ServiceLocator.TryGetService<ILocalizationService>(out var loc) && loc is not null)
            loc.RegisterResourceManager(Strings.ResourceManager);
    }
}

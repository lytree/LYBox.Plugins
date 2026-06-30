using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.Shared.Services;
using LYBox.Plugin.LayoutDisplay.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace LYBox.Plugin.LayoutDisplay;

[GenerateMetadata]
public partial class LayoutDisplayPlugin : IPluginMetadata
{
    public string Name => "Layout & Display Plugin";
    public string Version => "1.0.0";
    public string Author => "AvaloniaTemplate";
    public string Description => "Layout and display controls demo plugin.";
    public IEnumerable<string> Dependencies => [];
    public string PluginId => "LYBox.Plugin.LayoutDisplay";

    public Task InitializeAsync(IServiceCollection services) => Task.CompletedTask;

    public Task RegisterAsync(IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetService<ILocalizationService>() is { } loc)
            loc.RegisterResourceManager(Strings.ResourceManager);
        return Task.CompletedTask;
    }
}

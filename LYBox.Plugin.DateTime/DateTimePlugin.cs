using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.Shared.Services;
using LYBox.Plugin.DateTimeControls.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace LYBox.Plugin.DateTimeControls;

[GenerateMetadata]
public partial class DateTimePlugin : IPluginMetadata
{
    public string Name => "Date & Time Plugin";
    public string Version => "1.0.0";
    public string Author => "AvaloniaTemplate";
    public string Description => "Date and time controls demo plugin.";
    public IEnumerable<string> Dependencies => [];
    public string PluginId => "LYBox.Plugin.DateTime";

    public Task InitializeAsync(IServiceCollection services) => Task.CompletedTask;

    public Task RegisterAsync(IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetService<ILocalizationService>() is { } loc)
            loc.RegisterResourceManager(Strings.ResourceManager);
        return Task.CompletedTask;
    }
}

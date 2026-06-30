using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.Shared.Services;
using LYBox.Plugin.DialogFeedbacks.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace LYBox.Plugin.DialogFeedbacks;

[GenerateMetadata]
public partial class DialogFeedbacksPlugin : IPluginMetadata
{
    public string Name => "Dialog & Feedbacks Plugin";
    public string Version => "1.0.0";
    public string Author => "AvaloniaTemplate";
    public string Description => "Dialog and feedback controls demo plugin.";
    public IEnumerable<string> Dependencies => [];
    public string PluginId => "LYBox.Plugin.DialogFeedbacks";

    public Task InitializeAsync(IServiceCollection services) => Task.CompletedTask;

    public Task RegisterAsync(IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetService<ILocalizationService>() is { } loc)
            loc.RegisterResourceManager(Strings.ResourceManager);
        return Task.CompletedTask;
    }
}

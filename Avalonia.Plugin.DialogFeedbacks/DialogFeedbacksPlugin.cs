using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;

namespace Avalonia.Plugin.DialogFeedbacks;

[GenerateMetadata]
public partial class DialogFeedbacksPlugin : IPluginMetadata
{
    public string Name => "Dialog & Feedbacks Plugin";
    public string Version => "1.0.0";
    public string Author => "AvaloniaTemplate";
    public string Description => "Dialog and feedback controls demo plugin.";
    public IEnumerable<string> Dependencies => [];
    public string PluginId => "Avalonia.Plugin.DialogFeedbacks";

    public void Initialize()
    {
    }
}

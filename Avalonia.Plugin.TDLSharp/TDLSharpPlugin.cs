using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;

namespace Avalonia.Plugin.TDLSharp;

[GenerateMetadata]
public partial class TDLSharpPlugin : IPluginMetadata
{
    public string Name => "TDLSharp Plugin";
    public string Version => "1.0.0";
    public string Author => "TDLSharp";
    public string Description => "Telegram TDLib integration plugin providing batch forward, message export, media download and more.";
    public IEnumerable<string> Dependencies => [];
    public string PluginId => "A1B2C3D4-E5F6-7890-ABCD-TDLSHARP00001";

    public void Initialize()
    {
    }
}

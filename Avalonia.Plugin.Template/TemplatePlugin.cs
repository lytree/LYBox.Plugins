using Avalonia.Plugin.Shared;


namespace Avalonia.Plugin.Template;

public class TemplatePlugin : IPluginMetadata
{
    public string Name => "Template Plugin";
    public string Version => "1.0.0";

    public string Author => throw new NotImplementedException();

    public string Description => throw new NotImplementedException();

    public IEnumerable<string> Dependencies => throw new NotImplementedException();

    public string PluginId => throw new NotImplementedException();

    public void Initialize()
    {
        // 插件初始化逻辑
    }
}




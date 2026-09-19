using System.CommandLine;
using LYBox.Plugin.Shared.CommandLine;
using Spectre.Console;

namespace LYBox.Plugin.Template;

/// <summary>
/// CLI 命令注册示例：插件可以经宿主 CLI 注册 `template-aot hello --name=xxx` 子命令。
/// </summary>
public sealed class TemplateCliRegistrar : IPluginCommandRegistrar
{
    public string PluginId => "TEMPLATE-AOT-0000-0000-000000000000";
    public string CommandName => "template-aot";
    public string Description => "Native template plugin commands.";

    public void RegisterCommands(PluginCommandRegistrationContext context)
    {
        var name = new Option<string>("--name")
        {
            Description = "Name included in the greeting.",
            Required = true
        };
        name.Aliases.Add("-n");

        var hello = new Command("hello", "Create a greeting.");
        hello.Options.Add(name);
        hello.SetAction(parseResult =>
        {
            context.Console.WriteLine($"Hello, {parseResult.GetRequiredValue(name)}! (from native template)");
            return PluginCliExitCodes.Success;
        });
        context.Command.Subcommands.Add(hello);
    }
}
#!/usr/bin/env dotnet
#:sdk Cake.Sdk@6.2.0
#:package Spectre.Console@0.57.2
#:property PublishAot=false

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Cake.Common;
using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Build;
using Cake.Common.Tools.DotNet.MSBuild;
using Cake.Common.Tools.DotNet.Publish;
using Cake.Core;
using Cake.Core.Diagnostics;
using Spectre.Console;
// Disambiguate System.IO types from Cake.Core.IO.Path / Cake.Common helpers
using Path = System.IO.Path;
using File = System.IO.File;
using Directory = System.IO.Directory;
using Architecture = System.Runtime.InteropServices.Architecture;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS / CONTEXT
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var buildContext = new BuildContext(Context);

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(c =>
{
    var t = buildContext.Target;

    CleanDirectoryIfExists(c, buildContext.PluginPackagesDir);
    CleanDirectoryIfExists(c, buildContext.PluginZipPackagesDir);

    foreach (var plugin in buildContext.PluginProjects)
    {
        CleanDirectoryIfExists(c, Path.Combine(buildContext.PluginPackagesDir, plugin.ProjectName));
    }

    c.Log.Information("Clean completed. Target: {0}", t);

    static void CleanDirectoryIfExists(ICakeContext ctx, string dir)
    {
        if (!Directory.Exists(dir)) return;

        const int MaxAttempts = 4;
        Exception? lastError = null;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                ctx.CleanDirectory(dir);
                return;
            }
            catch (Exception ex) when (
                ex is IOException
                || ex is UnauthorizedAccessException
                || ex is DirectoryNotFoundException)
            {
                lastError = ex;
            }

            Thread.Sleep(150 * attempt);
        }

        ctx.Log.Warning(
            "Clean skipped after " + MaxAttempts + " attempts for '" + dir +
            "'. Files are still in use. The build will continue and downstream " +
            "targets will overwrite outputs. Last error: " + (lastError?.Message ?? "(unknown)"));
    }
});

Task("Build")
    .IsDependentOn("Clean")
    .Does(c =>
{
    var buildFailedPlugins = new List<string>();
    foreach (var plugin in buildContext.PluginProjects)
    {
        var pluginMsBuild = buildContext.CreatePluginMSBuildSettings(plugin);

        try
        {
            c.DotNetBuild(plugin.ProjectPath, new DotNetBuildSettings
            {
                Configuration = buildContext.BuildConfiguration,
                MSBuildSettings = pluginMsBuild
            });
            c.Log.Information("Plugin built: {0}", plugin.ProjectName);
        }
        catch (Exception ex)
        {
            c.Log.Error("插件 {0} 编译失败，跳过（不影响其他插件）: {1}", plugin.ProjectName, ex.Message);
            buildFailedPlugins.Add(plugin.ProjectName);
        }
    }
    if (buildFailedPlugins.Count > 0)
        throw new InvalidOperationException($"以下 {buildFailedPlugins.Count} 个插件编译失败: {string.Join(", ", buildFailedPlugins)}");

    c.Log.Information("Build completed. Target: {0}", buildContext.Target);
});

Task("PackPlugins")
    .IsDependentOn("Build")
    .Does(c =>
{
    c.EnsureDirectoryExists(buildContext.PluginPackagesDir);

    var failedPlugins = new List<string>();
    foreach (var plugin in buildContext.PluginProjects)
    {
        var pluginOutputDir = Path.Combine(buildContext.PluginPackagesDir, plugin.ProjectName, "publish");
        c.EnsureDirectoryExists(pluginOutputDir);

        var pluginMsBuild = buildContext.CreatePluginMSBuildSettings(plugin);

        try
        {
            c.DotNetPublish(plugin.ProjectPath, new DotNetPublishSettings
            {
                Configuration = buildContext.BuildConfiguration,
                OutputDirectory = pluginOutputDir,
                MSBuildSettings = pluginMsBuild
            });

            // 复制插件 wwwroot/ 前端资源到发布目录（仅当源目录存在时）
            CopyPluginWwwroot(c, buildContext, plugin, pluginOutputDir);

            c.Log.Information("Plugin published: {0} -> {1}", plugin.ProjectName, pluginOutputDir);
        }
        catch (Exception ex)
        {
            c.Log.Error("插件 {0} 发布失败，跳过（不影响其他插件）: {1}", plugin.ProjectName, ex.Message);
            failedPlugins.Add(plugin.ProjectName);
        }
    }

    PackPluginZips(c, buildContext);

    if (failedPlugins.Count > 0)
        throw new InvalidOperationException($"以下 {failedPlugins.Count} 个插件发布失败: {string.Join(", ", failedPlugins)}");

    c.Log.Information("All plugins published to: {0}", buildContext.PluginPackagesDir);

    static void CopyPluginWwwroot(ICakeContext ctx, BuildContext bctx, PluginProjectInfo plugin, string publishDir)
    {
        var pluginSrcDir = Path.Combine(bctx.RootDir, "plugins", plugin.ProjectName);
        var wwwrootSrc = Path.Combine(pluginSrcDir, "wwwroot");

        if (!Directory.Exists(wwwrootSrc))
        {
            ctx.Log.Debug("插件 {0} 无 wwwroot 目录，跳过前端资源复制", plugin.ProjectName);
            return;
        }

        var wwwrootDest = Path.Combine(publishDir, "wwwroot");
        CopyDirectoryRecursive(wwwrootSrc, wwwrootDest);
        ctx.Log.Information("插件 {0} wwwroot 已复制到 {1}", plugin.ProjectName, wwwrootDest);
    }

    static void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.TopDirectoryOnly))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }
        foreach (var subDir in Directory.GetDirectories(sourceDir, "*", SearchOption.TopDirectoryOnly))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
            CopyDirectoryRecursive(subDir, destSubDir);
        }
    }

    static void PackPluginZips(ICakeContext ctx, BuildContext bctx)
    {
        var zipOutputDir = bctx.PluginZipPackagesDir;
        ctx.EnsureDirectoryExists(zipOutputDir);

        foreach (var plugin in bctx.PluginProjects)
        {
            var publishDir = Path.Combine(bctx.PluginPackagesDir, plugin.ProjectName, "publish");

            if (!Directory.Exists(publishDir))
            {
                ctx.Log.Warning("Publish directory not found for plugin: {0}, skipping zip packaging", plugin.ProjectName);
                continue;
            }

            EnsurePluginManifest(publishDir, plugin, bctx, ctx);

            var effectiveVersion = bctx.GetEffectivePluginVersion(plugin);
            var zipPath = Path.Combine(zipOutputDir, $"{plugin.ProjectName}-{effectiveVersion}.zip");

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            using (var zipStream = new FileStream(zipPath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                foreach (var file in Directory.GetFiles(publishDir, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(publishDir, file);
                    var fileName = Path.GetFileName(file);

                    // 排除调试符号、文档注释、构建配置等运行时不需要的文件
                    var extension = Path.GetExtension(file);
                    if (extension.Equals(".pdb", StringComparison.OrdinalIgnoreCase) ||
                        extension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.Log.Debug("Skipping excluded file: {0}", relativePath);
                        continue;
                    }

                    // 排除 .deps.json、.runtimeconfig.json 等 SDK 生成的配置
                    if (fileName.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase) ||
                        fileName.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.Log.Debug("Skipping SDK generated config: {0}", relativePath);
                        continue;
                    }

                    var entry = archive.CreateEntry(relativePath);
                    using (var entryStream = entry.Open())
                    using (var fileStream = File.OpenRead(file))
                    {
                        fileStream.CopyTo(entryStream);
                    }
                }
            }

            ctx.Log.Information("Plugin zip created: {0}", zipPath);
        }

        ctx.Log.Information("All plugin zip packages created in: {0}", zipOutputDir);
    }

    static void EnsurePluginManifest(string publishDir, PluginProjectInfo plugin, BuildContext bctx, ICakeContext ctx)
    {
        var manifestPath = Path.Combine(publishDir, "plugin.json");
        if (File.Exists(manifestPath)) return;

        var mainDll = Path.Combine(publishDir, $"{plugin.ProjectName}.dll");
        var assemblyName = plugin.ProjectName;

        if (File.Exists(mainDll))
        {
            try
            {
                var asmName = System.Reflection.AssemblyName.GetAssemblyName(mainDll);
                assemblyName = asmName.Name ?? plugin.ProjectName;
            }
            catch { }
        }

        var effectiveVersion = bctx.GetEffectivePluginVersion(plugin);

        var json = $@"{{
  ""pluginId"": ""{plugin.PluginId}"",
  ""name"": ""{plugin.PluginName}"",
  ""version"": ""{effectiveVersion}"",
  ""author"": ""{plugin.PluginAuthor}"",
  ""description"": ""{plugin.PluginDescription}"",
  ""assembly"": ""{assemblyName}.dll"",
  ""dependencies"": [],
  ""minPluginSdkVersion"": ""{plugin.MinPluginSdkVersion}""
}}";
        File.WriteAllText(manifestPath, json);
    }
});

Task("Default")
    .IsDependentOn("PackPlugins");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);

//////////////////////////////////////////////////////////////////////
// SUPPORTING TYPES
//////////////////////////////////////////////////////////////////////

[Flags]
public enum BuildTarget
{
    None = 0,
    Plugin = 1,
    All = Plugin
}

/// <summary>
/// 包装 ICakeContext，集中管理构建参数、目录解析、SDK feed 版本注入与交互式提示。
/// </summary>
public class BuildContext
{
    private ICakeContext Cake { get; }

    public BuildTarget Target { get; }
    public string BuildConfiguration { get; }

    // 插件版本覆盖：--plugin-version 显式覆盖各插件 <PluginVersion>（优先级最高）
    public string? PluginVersionOverride { get; }

    // SDK 契约版本覆盖：--sdk-version 注入 /p:PluginSdkVersion（默认取 version.props <LyboxVersion>）
    public string? SdkVersionOverride { get; }

    // SDK feed 选择：--sdk-feed=local|nuget，--sdk-feed-path 显式指定本地目录，辅以 env:LYBOX_SDK_FEED
    public string SdkFeedMode { get; }
    public string SdkFeedPathOverride { get; }

    // 插件过滤：--plugin=<Name> 只构建匹配的插件（逗号分隔多个）
    public string? PluginFilter { get; }

    public string RootDir { get; }
    public string ArtifactsDir { get; }
    public string PluginPackagesDir { get; }
    public string PluginZipPackagesDir { get; }

    public IReadOnlyList<PluginProjectInfo> PluginProjects { get; }

    // 是否使用本地 SDK feed（else: 仅 nuget.org）
    public bool UseLocalFeed { get; }

    // 插件版本覆盖（优先级：--plugin-version > csproj <PluginVersion>）
    public DotNetMSBuildSettings CreatePluginMSBuildSettings(PluginProjectInfo plugin)
    {
        var settings = BaseSettings()
            .WithProperty("IsPluginProject", "true")
            .WithProperty("PluginId", plugin.PluginId)
            .WithProperty("PluginName", $"\"{plugin.PluginName}\"")
            .WithProperty("PluginAuthor", plugin.PluginAuthor)
            .WithProperty("PluginDescription", $"\"{plugin.PluginDescription}\"");

        // SDK 契约版本覆盖：注入全局属性 PluginSdkVersion，覆盖 version.props 默认值
        if (!string.IsNullOrEmpty(SdkVersionOverride))
            settings.WithProperty("PluginSdkVersion", SdkVersionOverride);

        if (!string.IsNullOrEmpty(PluginVersionOverride))
            settings.SetVersion(PluginVersionOverride);
        // 否则：不设 Version，让 csproj <Version>$(PluginVersion)</Version> 生效
        return settings;
    }

    private DotNetMSBuildSettings BaseSettings()
    {
        return new DotNetMSBuildSettings()
            .SetConfiguration(BuildConfiguration)
            .WithProperty("ContinuousIntegrationBuild", "true");
    }

    // 计算插件最终版本：--plugin-version > csproj <PluginVersion>
    public string GetEffectivePluginVersion(PluginProjectInfo plugin)
    {
        if (!string.IsNullOrEmpty(PluginVersionOverride))
            return PluginVersionOverride;
        return plugin.PluginVersion;
    }


    public BuildContext(ICakeContext context)
    {
        Cake = context;

        Target = ParseBuildTarget(context.Argument("build", ""));
        var requestedBuildConfiguration = context.Argument("configuration", "");
        BuildConfiguration = SelectBuildConfiguration(
            requestedBuildConfiguration,
            !string.IsNullOrWhiteSpace(requestedBuildConfiguration));

        PluginVersionOverride = context.Argument("plugin-version", "");
        SdkVersionOverride = context.Argument("sdk-version", "");
        SdkFeedMode = NormalizeSdkFeedMode(context.Argument("sdk-feed", ""));
        SdkFeedPathOverride = context.Argument("sdk-feed-path", "");
        PluginFilter = context.Argument("plugin", "");

        RootDir = ResolveRepositoryRoot();
        ArtifactsDir = Path.Combine(RootDir, "artifacts");
        PluginPackagesDir = Path.Combine(ArtifactsDir, "publish", "plugins");
        PluginZipPackagesDir = Path.Combine(ArtifactsDir, "packages", "plugins");

        UseLocalFeed = ResolveUseLocalFeed();

        // 依据设计约定：本地源路径经 %LYBOX_SDK_FEED% 环境变量注入 nuget.config。
        // 此处在进程级设置环境变量，使 --sdk-feed / --sdk-feed-path / 默认 staging 探测真正生效。
        if (UseLocalFeed)
            Environment.SetEnvironmentVariable("LYBOX_SDK_FEED", ResolveLocalFeedPath());

        var discoveredPlugins = DiscoverPlugins(RootDir);
        PluginProjects = SelectPluginFilters(Target, PluginFilter, discoveredPlugins);
    }

    public ICakeLog Log => Cake.Log;
    public void EnsureDirectoryExists(string path) => Cake.EnsureDirectoryExists(path);
    public void CleanDirectory(string path) => Cake.CleanDirectory(path);
    public void DotNetBuild(string project, DotNetBuildSettings settings) => Cake.DotNetBuild(project, settings);
    public void DotNetPublish(string project, DotNetPublishSettings settings) => Cake.DotNetPublish(project, settings);
    public IEnumerable<Cake.Core.IO.FilePath> GetFiles(string pattern) => Cake.GetFiles(pattern);

    private static string ResolveRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        var buildDirectory = Path.GetDirectoryName(sourceFilePath)
            ?? throw new InvalidOperationException("无法解析 build.cs 所在目录");
        return Path.GetFullPath(Path.Combine(buildDirectory, ".."));
    }

    private static string NormalizeSdkFeedMode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        return value.Trim().ToLowerInvariant() switch
        {
            "local" => "local",
            "nuget" => "nuget",
            _ => throw new ArgumentException($"Unknown sdk-feed mode: '{value}'. Valid values: local, nuget")
        };
    }

    // 解析是否使用本地 SDK feed：
    //   --sdk-feed=local        强制本地
    //   --sdk-feed=nuget        仅 nuget.org
    //   默认（未显式传）：env:LYBOX_SDK_FEED 已设 或 默认 staging(artifacts/packages/sdk) 含 *.nupkg → 本地，否则 nuget
    // 本地源路径由 nuget.config 的 %LYBOX_SDK_FEED% 与环境变量驱动；RestoreSources 不经 MSBuild 属性注入，
    // 以免值中分号 / https:// 被命令行拆分误判为相对路径（NU1301）。
    private bool ResolveUseLocalFeed()
    {
        if (SdkFeedMode == "nuget")
            return false;

        if (SdkFeedMode == "local")
            return true;

        var envFeed = Environment.GetEnvironmentVariable("LYBOX_SDK_FEED") ?? "";
        if (!string.IsNullOrWhiteSpace(envFeed))
            return true;

        var defaultLocal = Path.Combine(ArtifactsDir, "packages", "sdk");
        return Directory.Exists(defaultLocal) &&
               Directory.GetFiles(defaultLocal, "*.nupkg", SearchOption.TopDirectoryOnly).Length > 0;
    }

    // 本地 SDK feed 实际路径：--sdk-feed-path > env:LYBOX_SDK_FEED > 默认 artifacts/packages/sdk
    public string ResolveLocalFeedPath()
    {
        if (!string.IsNullOrWhiteSpace(SdkFeedPathOverride))
            return SdkFeedPathOverride;
        var envFeed = Environment.GetEnvironmentVariable("LYBOX_SDK_FEED") ?? "";
        if (!string.IsNullOrWhiteSpace(envFeed))
            return envFeed;
        return Path.Combine(ArtifactsDir, "packages", "sdk");
    }

    private static IReadOnlyList<PluginProjectInfo> FilterPlugins(IReadOnlyList<PluginProjectInfo> all, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return all;

        var names = new HashSet<string>(
            filter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);

        var matched = all.Where(p => names.Contains(p.ProjectName)).ToList();
        if (matched.Count == 0)
            throw new InvalidOperationException(
                $"--plugin 过滤无匹配项 '{filter}'。可用插件：{string.Join(", ", all.Select(p => p.ProjectName))}");

        return matched;
    }

    private static IReadOnlyList<PluginProjectInfo> DiscoverPlugins(string rootDir)
    {
        var pluginsDir = Path.Combine(rootDir, "plugins");
        if (!Directory.Exists(pluginsDir))
            return Array.Empty<PluginProjectInfo>();

        var plugins = new List<PluginProjectInfo>();

        foreach (var csprojFile in Directory.GetFiles(pluginsDir, "*.csproj", SearchOption.AllDirectories))
        {
            var projectName = Path.GetFileNameWithoutExtension(csprojFile);
            var doc = XDocument.Load(csprojFile);

            var pluginId = doc.Descendants("PluginId").FirstOrDefault()?.Value ?? projectName;
            var pluginName = doc.Descendants("PluginName").FirstOrDefault()?.Value ?? projectName;
            var pluginAuthor = doc.Descendants("PluginAuthor").FirstOrDefault()?.Value ?? "AvaloniaPlugin";
            var pluginDescription = doc.Descendants("PluginDescription").FirstOrDefault()?.Value ?? "";
            var pluginVersion = doc.Descendants("PluginVersion").FirstOrDefault()?.Value
                             ?? doc.Descendants("Version").FirstOrDefault()?.Value
                             ?? "1.0.0";
            var minPluginSdkVersion = doc.Descendants("MinPluginSdkVersion").FirstOrDefault()?.Value ?? "0.0.0";

            plugins.Add(new PluginProjectInfo(
                Path.GetFullPath(csprojFile), projectName, pluginId, pluginName, pluginVersion, pluginAuthor, pluginDescription, minPluginSdkVersion));
        }

        return plugins.OrderBy(plugin => plugin.ProjectPath, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static BuildTarget ParseBuildTarget(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return BuildTarget.All;

        var result = BuildTarget.None;
        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            result |= part.ToLowerInvariant() switch
            {
                "all" or "plugin" => BuildTarget.Plugin,
                _ => throw new ArgumentException($"Unknown build target: '{part}'. Valid values: all, plugin")
            };
        }
        return result == BuildTarget.None ? BuildTarget.All : result;
    }

    // ---- 交互式提示（未传参且终端可交互时触发）----

    private static bool CanPrompt => !Console.IsInputRedirected && !Console.IsOutputRedirected;

    private static IReadOnlyList<PluginProjectInfo> SelectPluginFilters(
        BuildTarget target,
        string? requestedFilter,
        IReadOnlyList<PluginProjectInfo> plugins)
    {
        if (!string.IsNullOrWhiteSpace(requestedFilter) || !CanPrompt)
            return FilterPlugins(plugins, requestedFilter);

        if (plugins.Count == 0)
            return plugins;

        AnsiConsole.Write(new Rule("[yellow]未指定 --plugin：请选择要打包的插件[/]")
            .RuleStyle("grey"));

        var mode = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("选择打包方式")
                .AddChoices("从列表选择", "输入插件名称", "构建全部插件"));

        return mode switch
        {
            "从列表选择" => FilterPlugins(plugins, string.Join(",", SelectPluginsFromList(plugins))),
            "输入插件名称" => FilterPlugins(plugins, PromptForPluginFilters(plugins)),
            "构建全部插件" => plugins,
            _ => throw new InvalidOperationException($"Unsupported plugin selection mode: {mode}"),
        };
    }

    private static IReadOnlyList<string> SelectPluginsFromList(IReadOnlyList<PluginProjectInfo> plugins)
    {
        var selected = AnsiConsole.Prompt(
            new MultiSelectionPrompt<PluginProjectInfo>()
                .Title("选择要打包的插件")
                .InstructionsText("[grey]使用 [blue]↑[/]/[blue]↓[/] 移动，按 [blue]Space[/] 勾选，按 [blue]Enter[/] 确认。[/]")
                .PageSize(Math.Min(plugins.Count, 10))
                .UseConverter(plugin => $"{plugin.ShortName} [grey]({plugin.ProjectName})[/]")
                .AddChoices(plugins));

        return selected.Select(plugin => plugin.ProjectName).ToArray();
    }

    private static string PromptForPluginFilters(IReadOnlyList<PluginProjectInfo> plugins)
    {
        while (true)
        {
            var input = AnsiConsole.Ask<string>("输入插件名称或序号（用逗号分隔；输入 [green]all[/] 构建全部）：");

            var selections = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (selections.Length == 0)
            {
                AnsiConsole.MarkupLine("[red]请输入至少一个插件。[/]");
                continue;
            }

            if (selections.Any(s => string.Equals(s, "all", StringComparison.OrdinalIgnoreCase)))
            {
                if (selections.Length > 1)
                {
                    AnsiConsole.MarkupLine("[red]'all' 不能与其他选项组合。[/]");
                    continue;
                }
                return "";
            }

            var filters = new List<string>();
            var valid = true;
            foreach (var selection in selections)
            {
                if (int.TryParse(selection, out var index))
                {
                    if (index < 1 || index > plugins.Count)
                    {
                        AnsiConsole.MarkupLine($"[red]插件序号 '{selection}' 超出范围。[/]");
                        valid = false;
                        break;
                    }
                    filters.Add(plugins[index - 1].ProjectName);
                }
                else
                {
                    if (!plugins.Any(p => p.Matches(selection)))
                    {
                        AnsiConsole.MarkupLine($"[red]未知插件 '{selection}'。[/]");
                        valid = false;
                        break;
                    }
                    filters.Add(selection);
                }
            }

            if (valid)
                return string.Join(",", filters);
        }
    }

    private static string SelectBuildConfiguration(string requestedConfiguration, bool isConfigured)
    {
        if (isConfigured || !CanPrompt)
            return string.IsNullOrWhiteSpace(requestedConfiguration) ? "Release" : requestedConfiguration;

        AnsiConsole.Write(new Rule("[yellow]未指定 --configuration：请选择构建配置[/]")
            .RuleStyle("grey"));

        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("选择构建配置")
                .AddChoices("Release", "Debug"));
    }
}

public record PluginProjectInfo(
    string ProjectPath,
    string ProjectName,
    string PluginId,
    string PluginName,
    string PluginVersion,
    string PluginAuthor,
    string PluginDescription,
    string MinPluginSdkVersion)
{
    public string ShortName =>
        ProjectName.StartsWith("LYBox.Plugin.", StringComparison.OrdinalIgnoreCase)
            ? ProjectName["LYBox.Plugin.".Length..]
            : ProjectName;

    public bool Matches(string value)
    {
        var key = NormalizePluginKey(value);
        return key == NormalizePluginKey(ProjectName)
            || key == NormalizePluginKey(ShortName)
            || key == NormalizePluginKey(PluginId)
            || key == NormalizePluginKey(PluginName);
    }

    private static string NormalizePluginKey(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }
}

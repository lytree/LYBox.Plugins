using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.TDLSharp.Models;

public enum ScriptParameterType
{
    String,
    Bool,
    Int
}

public partial class ScriptParameter : ObservableObject
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public ScriptParameterType ParameterType { get; init; }
    public bool IsRequired { get; init; }

    [ObservableProperty] private string? _defaultValue;

    public bool DefaultBoolValue
    {
        get => DefaultValue?.ToLower() == "true";
        set => DefaultValue = value ? "true" : "false";
    }

    public int DefaultIntValue
    {
        get => int.TryParse(DefaultValue, out var v) ? v : 0;
        set => DefaultValue = value.ToString();
    }

    public static ScriptParameter Text(string key, string displayName, string? description = null,
        string? defaultValue = null, bool required = false)
    {
        return new ScriptParameter
        {
            Key = key,
            DisplayName = displayName,
            Description = description,
            ParameterType = ScriptParameterType.String,
            DefaultValue = defaultValue,
            IsRequired = required
        };
    }

    public static ScriptParameter Switch(string key, string displayName, string? description = null,
        bool defaultValue = false)
    {
        return new ScriptParameter
        {
            Key = key,
            DisplayName = displayName,
            Description = description,
            ParameterType = ScriptParameterType.Bool,
            DefaultValue = defaultValue ? "true" : "false"
        };
    }

    public static ScriptParameter Number(string key, string displayName, string? description = null,
        int defaultValue = 0)
    {
        return new ScriptParameter
        {
            Key = key,
            DisplayName = displayName,
            Description = description,
            ParameterType = ScriptParameterType.Int,
            DefaultValue = defaultValue.ToString()
        };
    }
}

public class ScriptDescriptor
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public List<ScriptParameter> Parameters { get; init; } = [];
}

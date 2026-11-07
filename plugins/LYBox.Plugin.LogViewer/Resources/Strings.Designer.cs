#nullable enable
using System.Globalization;
using System.Resources;

namespace LYBox.Plugin.LogViewer.Resources;

/// <summary>
/// 插件本地化字符串访问器。XAML 端通过 <c>{DynamicResource KEY}</c> 访问同一份 resx，
/// C# 端通过 <see cref="Get"/> 访问（键缺失时返回键名本身，便于定位漏翻）。
/// </summary>
public static class Strings
{
    private static ResourceManager? _resourceManager;

    public static ResourceManager ResourceManager
    {
        get
        {
            if (_resourceManager is null)
            {
                _resourceManager = new ResourceManager(
                    "LYBox.Plugin.LogViewer.Resources.Strings",
                    typeof(Strings).Assembly);
            }

            return _resourceManager;
        }
    }

    private static CultureInfo? _culture;

    public static CultureInfo Culture
    {
        get => _culture ?? CultureInfo.CurrentUICulture;
        set => _culture = value;
    }

    public static string Get(string key) => ResourceManager.GetString(key, Culture) ?? key;

    public static string Format(string key, params object?[] args)
    {
        var template = Get(key);
        return args.Length == 0 ? template : string.Format(Culture, template, args);
    }

    public static string NAV_LogViewer => Get(nameof(NAV_LogViewer));
    public static string BTN_OpenFiles => Get(nameof(BTN_OpenFiles));
    public static string BTN_Reload => Get(nameof(BTN_Reload));
    public static string BTN_Export => Get(nameof(BTN_Export));
    public static string BTN_Clear => Get(nameof(BTN_Clear));
    public static string BTN_Follow => Get(nameof(BTN_Follow));
    public static string LBL_NoSources => Get(nameof(LBL_NoSources));
    public static string LBL_NoEntries => Get(nameof(LBL_NoEntries));
    public static string LBL_NoSelection => Get(nameof(LBL_NoSelection));
    public static string STATUS_Ready => Get(nameof(STATUS_Ready));
    public static string STATUS_Loading => Get(nameof(STATUS_Loading));
}

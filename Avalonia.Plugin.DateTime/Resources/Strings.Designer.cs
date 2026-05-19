#nullable enable
namespace Avalonia.Plugin.DateTimeControls.Resources;

public static class Strings
{
    private static global::System.Resources.ResourceManager? _resourceManager;

    public static global::System.Resources.ResourceManager ResourceManager
    {
        get
        {
            if (_resourceManager is null)
            {
                _resourceManager = new global::System.Resources.ResourceManager(
                    "Avalonia.Plugin.DateTimeControls.Resources.Strings",
                    typeof(Strings).Assembly);
            }
            return _resourceManager;
        }
    }

    private static global::System.Globalization.CultureInfo? _culture;

    public static global::System.Globalization.CultureInfo Culture
    {
        get => _culture ?? global::System.Globalization.CultureInfo.CurrentUICulture;
        set => _culture = value;
    }

    public static string NAV_DateTime => ResourceManager.GetString(nameof(NAV_DateTime), Culture)!;
    public static string NAV_DatePicker => ResourceManager.GetString(nameof(NAV_DatePicker), Culture)!;
    public static string NAV_DateTimePicker => ResourceManager.GetString(nameof(NAV_DateTimePicker), Culture)!;
    public static string NAV_DateRangePicker => ResourceManager.GetString(nameof(NAV_DateRangePicker), Culture)!;
    public static string NAV_TimePicker => ResourceManager.GetString(nameof(NAV_TimePicker), Culture)!;
    public static string NAV_TimeRangePicker => ResourceManager.GetString(nameof(NAV_TimeRangePicker), Culture)!;
    public static string NAV_TimeBox => ResourceManager.GetString(nameof(NAV_TimeBox), Culture)!;
    public static string NAV_Clock => ResourceManager.GetString(nameof(NAV_Clock), Culture)!;
}

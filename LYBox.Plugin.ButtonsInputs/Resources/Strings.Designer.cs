#nullable enable
namespace LYBox.Plugin.ButtonsInputs.Resources;

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
                    "LYBox.Plugin.ButtonsInputs.Resources.Strings",
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

    public static string NAV_ButtonsInputs => ResourceManager.GetString(nameof(NAV_ButtonsInputs), Culture)!;
    public static string NAV_ButtonGroup => ResourceManager.GetString(nameof(NAV_ButtonGroup), Culture)!;
    public static string NAV_IconButton => ResourceManager.GetString(nameof(NAV_IconButton), Culture)!;
    public static string NAV_AutoCompleteBox => ResourceManager.GetString(nameof(NAV_AutoCompleteBox), Culture)!;
    public static string NAV_ClassInput => ResourceManager.GetString(nameof(NAV_ClassInput), Culture)!;
    public static string NAV_EnumSelector => ResourceManager.GetString(nameof(NAV_EnumSelector), Culture)!;
    public static string NAV_Form => ResourceManager.GetString(nameof(NAV_Form), Culture)!;
    public static string NAV_KeyGestureInput => ResourceManager.GetString(nameof(NAV_KeyGestureInput), Culture)!;
    public static string NAV_IPv4Box => ResourceManager.GetString(nameof(NAV_IPv4Box), Culture)!;
    public static string NAV_MultiComboBox => ResourceManager.GetString(nameof(NAV_MultiComboBox), Culture)!;
    public static string NAV_MultiAutoCompleteBox => ResourceManager.GetString(nameof(NAV_MultiAutoCompleteBox), Culture)!;
    public static string NAV_NumericUpDown => ResourceManager.GetString(nameof(NAV_NumericUpDown), Culture)!;
    public static string NAV_NumPad => ResourceManager.GetString(nameof(NAV_NumPad), Culture)!;
    public static string NAV_PathPicker => ResourceManager.GetString(nameof(NAV_PathPicker), Culture)!;
    public static string NAV_PinCode => ResourceManager.GetString(nameof(NAV_PinCode), Culture)!;
    public static string NAV_RangeSlider => ResourceManager.GetString(nameof(NAV_RangeSlider), Culture)!;
    public static string NAV_Rating => ResourceManager.GetString(nameof(NAV_Rating), Culture)!;
    public static string NAV_SelectionList => ResourceManager.GetString(nameof(NAV_SelectionList), Culture)!;
    public static string NAV_TagInput => ResourceManager.GetString(nameof(NAV_TagInput), Culture)!;
    public static string NAV_ThemeToggler => ResourceManager.GetString(nameof(NAV_ThemeToggler), Culture)!;
    public static string NAV_TreeComboBox => ResourceManager.GetString(nameof(NAV_TreeComboBox), Culture)!;
}

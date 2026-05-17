using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Plugin.TDLSharp.Models;
using System.Globalization;

namespace Avalonia.Plugin.TDLSharp.Models;

public class ScriptParameterTypeToVisibility : IValueConverter
{
    public static readonly IValueConverter StringVisibility = new ScriptParameterTypeToVisibility(ScriptParameterType.String);
    public static readonly IValueConverter BoolVisibility = new ScriptParameterTypeToVisibility(ScriptParameterType.Bool);
    public static readonly IValueConverter IntVisibility = new ScriptParameterTypeToVisibility(ScriptParameterType.Int);

    private readonly ScriptParameterType _target;

    private ScriptParameterTypeToVisibility(ScriptParameterType target)
    {
        _target = target;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ScriptParameterType pt)
        {
            return pt == _target;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}

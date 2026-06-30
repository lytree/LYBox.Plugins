using System.Globalization;
using Avalonia.Data.Converters;
using LYBox.Plugin.TDLSharp.ViewModels;

namespace LYBox.Plugin.TDLSharp.ViewModels;

public class AuthStepToVisibilityConverter : IValueConverter
{
    public static readonly AuthStepToVisibilityConverter OtherDevice = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is AuthStep step)
        {
            return step == AuthStep.WaitOtherDeviceConfirmation;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

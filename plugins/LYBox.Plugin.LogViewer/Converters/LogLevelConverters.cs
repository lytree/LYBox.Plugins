using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using LYBox.Plugin.LogViewer.Models;

namespace LYBox.Plugin.LogViewer.Converters;

/// <summary>
/// 日志级别配色。徽章用实色、行背景用低透明度叠色，
/// 这样在浅色与深色主题下都能保持可读，且不需要为每个主题各维护一套画刷。
/// </summary>
public static class LogLevelPalette
{
    private static readonly Dictionary<LogLevel, IBrush> s_badge = new()
    {
        [LogLevel.Trace] = Solid("#6E7781"),
        [LogLevel.Debug] = Solid("#8B949E"),
        [LogLevel.Info] = Solid("#2F81F7"),
        [LogLevel.Warn] = Solid("#BF8700"),
        [LogLevel.Error] = Solid("#DA3633"),
        [LogLevel.Fatal] = Solid("#8250DF"),
        [LogLevel.Unknown] = Solid("#6E7781"),
    };

    private static readonly Dictionary<LogLevel, IBrush> s_row = new()
    {
        [LogLevel.Trace] = Solid("#0E6E7781"),
        [LogLevel.Debug] = Solid("#0E8B949E"),
        [LogLevel.Info] = Solid("#102F81F7"),
        [LogLevel.Warn] = Solid("#1FBF8700"),
        [LogLevel.Error] = Solid("#1FDA3633"),
        [LogLevel.Fatal] = Solid("#248250DF"),
        [LogLevel.Unknown] = Solid("#00000000"),
    };

    private static readonly Dictionary<LogLevel, IBrush> s_text = new()
    {
        [LogLevel.Trace] = Solid("#6E7781"),
        [LogLevel.Debug] = Solid("#8B949E"),
        [LogLevel.Info] = Solid("#2F81F7"),
        [LogLevel.Warn] = Solid("#BF8700"),
        [LogLevel.Error] = Solid("#DA3633"),
        [LogLevel.Fatal] = Solid("#8250DF"),
        [LogLevel.Unknown] = Solid("#6E7781"),
    };

    public static IBrush Badge(LogLevel level) => s_badge.TryGetValue(level, out var brush) ? brush : Brushes.Transparent;

    public static IBrush Row(LogLevel level) => s_row.TryGetValue(level, out var brush) ? brush : Brushes.Transparent;

    public static IBrush Text(LogLevel level) => s_text.TryGetValue(level, out var brush) ? brush : Brushes.Gray;

    private static IBrush Solid(string hex) => new SolidColorBrush(Color.Parse(hex)).ToImmutable();
}

/// <summary>级别 → 徽章背景色。</summary>
public sealed class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is LogLevel level ? LogLevelPalette.Badge(level) : Brushes.Transparent;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>级别 → 行背景色（低透明度）。</summary>
public sealed class LogLevelToRowBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is LogLevel level ? LogLevelPalette.Row(level) : Brushes.Transparent;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>级别 → 文本前景色（消息列着色）。</summary>
public sealed class LogLevelToTextBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is LogLevel level ? LogLevelPalette.Text(level) : Brushes.Gray;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

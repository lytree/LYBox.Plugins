using System.Collections.ObjectModel;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace Avalonia.Plugin.ButtonsInputs.ViewModels;


public partial class EnumSelectorDemoViewModel : ObservableObject
{
    [ObservableProperty] private Type? _selectedType;
    [ObservableProperty] private object? _value;

    public ObservableCollection<Type?> Types { get; set; } =
    [
        typeof(HorizontalAlignment),
        typeof(VerticalAlignment),
        typeof(Orientation),
        typeof(Dock),
        typeof(GridResizeDirection),
        typeof(DayOfWeek),
        typeof(FillMode),
        typeof(IterationType),
        typeof(BindingMode),
        typeof(BindingPriority),
        typeof(StandardCursorType),
        typeof(Key),
        typeof(KeyModifiers),
        typeof(RoutingStrategies),
        typeof(CustomEnum)
    ];
}

public enum CustomEnum
{
    [Description("是")] Yes,
    [Description("否")] No,
}






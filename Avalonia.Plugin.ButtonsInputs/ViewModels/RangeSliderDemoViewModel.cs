using System.Collections.ObjectModel;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Layout;

namespace Avalonia.Plugin.ButtonsInputs.ViewModels;

[Menu("RangeSlider", MenuKeys.MenuKeyRangeSlider)]
public partial class RangeSliderDemoViewModel: ObservableObject
{
    public ObservableCollection<Orientation> Orientations { get; set; } = new ObservableCollection<Orientation>()
    {
        Orientation.Horizontal,
        Orientation.Vertical
    };

    [ObservableProperty] private Orientation _orientation;
}






using System.Collections.ObjectModel;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.ButtonsInputs.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Layout;

namespace Avalonia.Plugin.ButtonsInputs.ViewModels;

[NavigationItem("RangeSlider")]
[Menu("RangeSlider", "RangeSlider", "ButtonsInputs")]
[ViewMap(typeof(RangeSliderDemo))]
public partial class RangeSliderDemoViewModel: ObservableObject
{
    public ObservableCollection<Orientation> Orientations { get; set; } = new ObservableCollection<Orientation>()
    {
        Orientation.Horizontal,
        Orientation.Vertical
    };

    [ObservableProperty] private Orientation _orientation;
}






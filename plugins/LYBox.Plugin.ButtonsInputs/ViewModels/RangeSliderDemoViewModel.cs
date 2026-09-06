using System.Collections.ObjectModel;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.ButtonsInputs.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Layout;

namespace LYBox.Plugin.ButtonsInputs.ViewModels;

[NavigationItem("RangeSlider")]
[Menu("NAV_RangeSlider", "RangeSlider", "NAV_ButtonsInputs")]
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






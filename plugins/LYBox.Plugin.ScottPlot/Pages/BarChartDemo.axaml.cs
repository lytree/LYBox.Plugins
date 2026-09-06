using Avalonia.Controls;
using LYBox.Plugin.ScottPlot.Controls;
using LYBox.Plugin.ScottPlot.ViewModels;

namespace LYBox.Plugin.ScottPlot.Pages;

public partial class BarChartDemo : UserControl
{
    public BarChartDemo()
    {
        InitializeComponent();
        var vm = new BarChartDemoViewModel();
        DataContext = vm;
        var plotView = this.FindControl<PlotView>("PlotView")!;
        plotView.Plot = vm.Plot;
        vm.PlotChanged += () => plotView.Refresh();
    }
}

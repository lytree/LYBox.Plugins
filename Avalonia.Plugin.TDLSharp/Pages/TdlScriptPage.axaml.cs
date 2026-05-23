using Avalonia.Controls;
using Avalonia.Plugin.TDLSharp.ViewModels;
using Avalonia.Threading;
using System.ComponentModel;

namespace Avalonia.Plugin.TDLSharp.Pages;

public partial class TdlScriptPage : UserControl
{
    private TdlViewModelBase? _currentVm;

    public TdlScriptPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_currentVm is not null)
        {
            _currentVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _currentVm = DataContext as TdlViewModelBase;

        if (_currentVm is not null)
        {
            _currentVm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TdlViewModelBase.LogText))
        {
            Dispatcher.UIThread.Post(() =>
            {
                LogScrollViewer.ScrollToEnd();
            }, DispatcherPriority.Loaded);
        }
    }
}

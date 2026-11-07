using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LYBox.Plugin.LogViewer.Models;
using LYBox.Plugin.LogViewer.ViewModels;

namespace LYBox.Plugin.LogViewer.Pages;

public partial class LogViewerPage : UserControl
{
    private LogViewerPageViewModel? _viewModel;

    public LogViewerPage()
    {
        InitializeComponent();

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.ScrollToEndRequested -= OnScrollToEndRequested;
        }

        _viewModel = DataContext as LogViewerPageViewModel;

        if (_viewModel is not null)
        {
            _viewModel.ScrollToEndRequested += OnScrollToEndRequested;
        }
    }

    private void OnScrollToEndRequested(object? sender, EventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(
            () =>
            {
                var count = _viewModel.VisibleEntries.Count;
                if (count > 0)
                {
                    LogList.ScrollIntoView(count - 1);
                }
            },
            DispatcherPriority.Background);
    }

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var files = e.DataTransfer.TryGetFiles();
        if (files is null)
        {
            return;
        }

        var paths = files
            .Select(f => f.TryGetLocalPath())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .ToArray();

        if (paths.Length > 0)
        {
            _ = _viewModel.LoadFilesAsync(paths);
        }

        e.Handled = true;
    }

    private void OnCopyClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var selected = LogList.SelectedItems?.OfType<LogEntry>().ToList() ?? [];
        if (selected.Count == 0)
        {
            return;
        }

        _viewModel.CopyTextToClipboard(_viewModel.FormatEntries(selected));
        _viewModel.ReportCopied(selected.Count);
    }
}

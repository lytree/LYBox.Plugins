using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Plugin.TDLSharp.Models;
using Avalonia.Plugin.TDLSharp.ViewModels;

namespace Avalonia.Plugin.TDLSharp.Controls;

public partial class ExecutionHistoryDialog : UserControl
{
    public ExecutionHistoryDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // 弹框尺寸固定为父窗口的60%
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            Width = window.Bounds.Width * 0.6;
            Height = window.Bounds.Height * 0.6;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not ExecutionHistoryDialogViewModel vm) return;

        HistoryDataGrid.Columns.Clear();

        foreach (var colDef in vm.ColumnDefinitions)
        {
            DataGridTextColumn column;

            if (colDef.IsParameter)
            {
                // 参数列：通过 GetParameterValue(key) 绑定
                column = new DataGridTextColumn
                {
                    Header = colDef.Header,
                    Binding = new Avalonia.Data.Binding($"GetParameterValue({colDef.BindingKey})"),
                };
            }
            else
            {
                // 固定列：直接属性绑定
                var bindingPath = colDef.BindingKey switch
                {
                    "ExecutedAt" => "ExecutedAt",
                    "DurationText" => "DurationText",
                    "Status" => "Status",
                    "ErrorMessage" => "ErrorMessage",
                    "ParameterSummary" => "ParameterSummary",
                    _ => colDef.BindingKey
                };

                var binding = new Avalonia.Data.Binding(bindingPath);

                if (colDef.BindingKey == "ExecutedAt")
                {
                    binding.StringFormat = "{}{0:yyyy-MM-dd HH:mm:ss}";
                }

                column = new DataGridTextColumn
                {
                    Header = colDef.Header,
                    Binding = binding,
                };
            }

            // 设置列宽
            column.Width = colDef.Width switch
            {
                "*" => new DataGridLength(1, DataGridLengthUnitType.Star),
                "Auto" => DataGridLength.Auto,
                _ => new DataGridLength(double.Parse(colDef.Width))
            };

            HistoryDataGrid.Columns.Add(column);
        }
    }

    private void DataGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is DataGrid grid && grid.SelectedItem is not null)
        {
            if (DataContext is ExecutionHistoryDialogViewModel vm)
            {
                vm.ApplyParametersCommand.Execute(grid.SelectedItem);
            }
        }
    }
}

using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Plugin.TDLSharp.Services;

namespace Avalonia.Plugin.TDLSharp.Controls;

public partial class HistoryComboBox : UserControl
{
    private readonly ObservableCollection<string> _historyItems = [];
    private bool _suppressSave;
    private string _lastSavedText = string.Empty;

    /// <summary>
    /// 当前输入文本
    /// </summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<HistoryComboBox, string>(nameof(Text), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// 占位提示文本
    /// </summary>
    public static readonly StyledProperty<string> PlaceholderTextProperty =
        AvaloniaProperty.Register<HistoryComboBox, string>(nameof(PlaceholderText));

    public string PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    /// <summary>
    /// 历史记录分组 Key，用于持久化区分不同输入框的历史
    /// </summary>
    public static readonly StyledProperty<string> HistoryKeyProperty =
        AvaloniaProperty.Register<HistoryComboBox, string>(nameof(HistoryKey));

    public string HistoryKey
    {
        get => GetValue(HistoryKeyProperty);
        set => SetValue(HistoryKeyProperty, value);
    }

    /// <summary>
    /// 最大历史记录条数
    /// </summary>
    public static readonly StyledProperty<int> MaxHistoryCountProperty =
        AvaloniaProperty.Register<HistoryComboBox, int>(nameof(MaxHistoryCount), 50);

    public int MaxHistoryCount
    {
        get => GetValue(MaxHistoryCountProperty);
        set => SetValue(MaxHistoryCountProperty, value);
    }

    static HistoryComboBox()
    {
        HistoryKeyProperty.Changed.AddClassHandler<HistoryComboBox>((x, _) => x.LoadHistory());
    }

    public HistoryComboBox()
    {
        InitializeComponent();
        HistoryList.ItemsSource = _historyItems;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        ToggleDropdown.Click += OnToggleDropdown;
        ClearAllButton.Click += OnClearAll;
        HistoryList.SelectionChanged += OnHistoryItemSelected;
        InputBox.KeyDown += OnInputKeyDown;
        InputBox.LostFocus += OnInputLostFocus;

        LoadHistory();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        // 卸载时保存当前文本
        SaveCurrentText();

        ToggleDropdown.Click -= OnToggleDropdown;
        ClearAllButton.Click -= OnClearAll;
        HistoryList.SelectionChanged -= OnHistoryItemSelected;
        InputBox.KeyDown -= OnInputKeyDown;
        InputBox.LostFocus -= OnInputLostFocus;
    }

    private void OnToggleDropdown(object? sender, RoutedEventArgs e)
    {
        if (HistoryPopup.IsOpen)
        {
            HistoryPopup.Close();
        }
        else
        {
            RefreshFilteredHistory();
            HistoryPopup.Open();
            InputBox.Focus();
        }
    }

    private void OnHistoryItemSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (HistoryList.SelectedItem is string selected)
        {
            // 选择历史项时抑制保存，避免重复写入
            _suppressSave = true;
            Text = selected;
            _lastSavedText = selected;
            _suppressSave = false;

            HistoryPopup.Close();
            HistoryList.SelectedItem = null;
            InputBox.Focus();
        }
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SaveCurrentText();
            HistoryPopup.Close();
        }
        else if (e.Key == Key.Down && !HistoryPopup.IsOpen)
        {
            RefreshFilteredHistory();
            if (_historyItems.Count > 0)
            {
                HistoryPopup.Open();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Escape)
        {
            HistoryPopup.Close();
        }
    }

    private void OnInputLostFocus(object? sender, RoutedEventArgs e)
    {
        if (!_suppressSave)
            SaveCurrentText();
    }

    private void OnClearAll(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(HistoryKey)) return;
        InputHistoryService.Instance.ClearHistory(HistoryKey);
        _historyItems.Clear();
        HistoryPopup.Close();
    }

    private void SaveCurrentText()
    {
        var text = Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(HistoryKey)) return;
        if (text == _lastSavedText) return;

        _lastSavedText = text;
        InputHistoryService.Instance.AddHistory(HistoryKey, text);
    }

    private void LoadHistory()
    {
        if (string.IsNullOrEmpty(HistoryKey)) return;

        _historyItems.Clear();
        foreach (var item in InputHistoryService.Instance.GetHistory(HistoryKey, MaxHistoryCount))
        {
            _historyItems.Add(item);
        }
    }

    private void RefreshFilteredHistory()
    {
        var filter = Text?.Trim() ?? string.Empty;
        var allItems = string.IsNullOrEmpty(HistoryKey)
            ? []
            : InputHistoryService.Instance.GetHistory(HistoryKey, MaxHistoryCount);

        _historyItems.Clear();
        var filtered = string.IsNullOrEmpty(filter)
            ? allItems
            : allItems.Where(h => h.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var item in filtered)
        {
            _historyItems.Add(item);
        }
    }
}

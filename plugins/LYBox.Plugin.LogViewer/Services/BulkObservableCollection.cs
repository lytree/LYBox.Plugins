using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace LYBox.Plugin.LogViewer.Services;

/// <summary>
/// 支持批量替换 / 批量追加的 ObservableCollection。
/// 逐条 Add 十万行会触发十万次通知（UI 线程被拖死），这里合并成一次 Reset / Add 通知。
/// </summary>
public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotification;

    /// <summary>用新内容整体替换（一次 Reset 通知）。</summary>
    public void ReplaceAll(IEnumerable<T> items)
    {
        _suppressNotification = true;
        try
        {
            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }
        }
        finally
        {
            _suppressNotification = false;
        }

        RaiseReset();
    }

    /// <summary>批量追加（一次 Add 通知，保留虚拟化滚动位置）。</summary>
    public void AppendRange(IReadOnlyList<T> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        var start = Count;

        _suppressNotification = true;
        try
        {
            foreach (var item in items)
            {
                Items.Add(item);
            }
        }
        finally
        {
            _suppressNotification = false;
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Add,
            items.ToList(),
            start));
    }

    private void RaiseReset()
    {
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (_suppressNotification)
        {
            return;
        }

        base.OnCollectionChanged(e);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (_suppressNotification)
        {
            return;
        }

        base.OnPropertyChanged(e);
    }
}

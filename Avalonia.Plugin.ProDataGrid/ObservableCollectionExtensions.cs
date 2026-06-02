using System.Collections.ObjectModel;

namespace Avalonia.Plugin.ProDataGrid;

/// <summary>
/// ObservableCollection 扩展方法，提供高效的集合同步操作。
/// </summary>
internal static class ObservableCollectionExtensions
{
    /// <summary>
    /// 将 ObservableCollection 与目标列表同步：移除多余项、插入缺失项。
    /// 相比 Clear() + 逐个 Add()，减少不必要的 UI 刷新次数。
    /// </summary>
    public static void SyncWith<T>(this ObservableCollection<T> source, IList<T> target)
    {
        // 快速路径：完全一致则跳过
        if (source.Count == target.Count)
        {
            bool same = true;
            for (int i = 0; i < source.Count; i++)
            {
                if (!Equals(source[i], target[i]))
                {
                    same = false;
                    break;
                }
            }
            if (same) return;
        }

        // 移除不在目标中的项（从后往前避免索引偏移）
        for (int i = source.Count - 1; i >= 0; i--)
        {
            if (!target.Contains(source[i]))
                source.RemoveAt(i);
        }

        // 插入目标中缺失的项
        for (int i = 0; i < target.Count; i++)
        {
            if (i >= source.Count)
            {
                source.Add(target[i]);
            }
            else if (!Equals(source[i], target[i]))
            {
                source.Insert(i, target[i]);
            }
        }
    }
}

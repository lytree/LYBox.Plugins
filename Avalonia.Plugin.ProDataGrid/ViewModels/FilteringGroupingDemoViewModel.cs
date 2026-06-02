using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.ProDataGrid.Models;
using Avalonia.Plugin.ProDataGrid.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Avalonia.Plugin.ProDataGrid.ViewModels;

[NavigationItem("KeyFilteringGrouping")]
[Menu("NAV_FilteringGrouping", "KeyFilteringGrouping", "NAV_ProDataGrid")]
[ViewMap(typeof(FilteringGroupingDemo))]
public partial class FilteringGroupingDemoViewModel : ObservableObject
{
    private static readonly Random _random = new();
    private int _nextId = 1;
    private List<TaskItem> _allTasks;

    public ObservableCollection<TaskItem> Tasks { get; }

    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private TaskItem? _selectedTask;

    public ObservableCollection<string> GroupByOptions { get; } = ["无", "状态", "优先级", "分类", "负责人"];

    [ObservableProperty] private string _groupByProperty = "无";

    public FilteringGroupingDemoViewModel()
    {
        _allTasks = GenerateTasks(50);
        Tasks = new ObservableCollection<TaskItem>(_allTasks);
    }

    partial void OnFilterTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnGroupByPropertyChanged(string value)
    {
        ApplyFilter();
    }

    [RelayCommand]
    private void AddTask()
    {
        var task = CreateRandomTask();
        _allTasks.Add(task);
        if (string.IsNullOrWhiteSpace(FilterText) || MatchesFilter(task))
            Tasks.Add(task);
    }

    [RelayCommand]
    private void ResetData()
    {
        _nextId = 1;
        _allTasks = GenerateTasks(50);
        FilterText = string.Empty;
        GroupByProperty = "无";
        Tasks.Clear();
        foreach (var t in _allTasks)
            Tasks.Add(t);
    }

    [RelayCommand]
    private void ClearFilter()
    {
        FilterText = string.Empty;
    }

    [RelayCommand]
    private void Save()
    {
        ExportHelper.ExportToJson(Tasks, "filtered_tasks_export.json");
    }

    private void ApplyFilter()
    {
        IEnumerable<TaskItem> filtered = string.IsNullOrWhiteSpace(FilterText)
            ? _allTasks
            : _allTasks.Where(MatchesFilter);

        if (GroupByProperty != "无")
        {
            filtered = GroupByProperty switch
            {
                "状态" => filtered.OrderBy(t => t.Status),
                "优先级" => filtered.OrderBy(t => t.Priority),
                "分类" => filtered.OrderBy(t => t.Category),
                "负责人" => filtered.OrderBy(t => t.Assignee),
                _ => filtered
            };
        }

        Tasks.SyncWith(filtered.ToList());
    }

    private bool MatchesFilter(TaskItem task)
    {
        var filter = FilterText.Trim();
        return task.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || task.Assignee.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || task.Status.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || task.Priority.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || task.Category.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private List<TaskItem> GenerateTasks(int count)
    {
        var list = new List<TaskItem>(count);
        for (int i = 0; i < count; i++)
            list.Add(CreateRandomTask());
        return list;
    }

    private TaskItem CreateRandomTask()
    {
        var status = TestData.Statuses[_random.Next(TestData.Statuses.Length)];
        var priority = TestData.Priorities[_random.Next(TestData.Priorities.Length)];
        var progress = status switch
        {
            "已完成" => 100.0,
            "审核中" => Math.Round(_random.NextDouble() * 20 + 80, 1),
            "进行中" => Math.Round(_random.NextDouble() * 70 + 10, 1),
            "待开始" => 0.0,
            _ => Math.Round(_random.NextDouble() * 100, 1)
        };
        var dueDate = priority switch
        {
            "紧急" => DateTime.Now.AddDays(_random.Next(1, 5)),
            "高" => DateTime.Now.AddDays(_random.Next(3, 14)),
            "中" => DateTime.Now.AddDays(_random.Next(7, 30)),
            "低" => DateTime.Now.AddDays(_random.Next(14, 60)),
            _ => DateTime.Now.AddDays(_random.Next(7, 30))
        };

        return new TaskItem(
            _nextId++,
            TestData.TaskTitles[_random.Next(TestData.TaskTitles.Length)],
            TestData.Assignees[_random.Next(TestData.Assignees.Length)],
            status, priority, progress, dueDate,
            TestData.TaskCategories[_random.Next(TestData.TaskCategories.Length)],
            TestData.TaskDescriptions[_random.Next(TestData.TaskDescriptions.Length)]
        );
    }
}

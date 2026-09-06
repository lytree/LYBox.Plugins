using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.ProDataGrid.Models;
using LYBox.Plugin.ProDataGrid.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace LYBox.Plugin.ProDataGrid.ViewModels;

[NavigationItem("KeyDragDrop")]
[Menu("NAV_DragDrop", "KeyDragDrop", "NAV_ProDataGrid")]
[ViewMap(typeof(DragDropDemo))]
public partial class DragDropDemoViewModel : ObservableObject
{
    private static readonly Random _random = new();
    private int _nextId = 1;

    public ObservableCollection<TaskItem> Tasks { get; }
    public ObservableCollection<TaskItem> SelectedTasks { get; } = [];

    [ObservableProperty] private bool _canReorderRows = true;
    [ObservableProperty] private string _dropInfo = string.Empty;
    [ObservableProperty] private TaskItem? _selectedTask;

    public DragDropDemoViewModel()
    {
        Tasks = new ObservableCollection<TaskItem>(GenerateTasks(25));
    }

    [RelayCommand]
    private void AddTask()
    {
        Tasks.Add(CreateRandomTask());
    }

    [RelayCommand]
    private void ResetData()
    {
        Tasks.Clear();
        _nextId = 1;
        foreach (var task in GenerateTasks(25))
            Tasks.Add(task);
        DropInfo = string.Empty;
    }

    [RelayCommand]
    private void MoveToTop()
    {
        if (SelectedTasks.Count == 0) return;
        foreach (var task in SelectedTasks.ToList())
        {
            Tasks.Remove(task);
            Tasks.Insert(0, task);
        }
        DropInfo = $"已将 {SelectedTasks.Count} 项移至顶部";
    }

    [RelayCommand]
    private void MoveToBottom()
    {
        if (SelectedTasks.Count == 0) return;
        foreach (var task in SelectedTasks.ToList())
        {
            Tasks.Remove(task);
        }
        foreach (var task in SelectedTasks)
        {
            Tasks.Add(task);
        }
        DropInfo = $"已将 {SelectedTasks.Count} 项移至底部";
    }

    [RelayCommand]
    private void Save()
    {
        ExportHelper.ExportToJson(Tasks, "tasks_export.json");
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
            string.Empty
        );
    }
}

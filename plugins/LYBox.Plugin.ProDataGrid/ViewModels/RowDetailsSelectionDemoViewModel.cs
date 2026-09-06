using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.ProDataGrid.Models;
using LYBox.Plugin.ProDataGrid.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace LYBox.Plugin.ProDataGrid.ViewModels;

[NavigationItem("KeyRowDetailsSelection")]
[Menu("NAV_RowDetailsSelection", "KeyRowDetailsSelection", "NAV_ProDataGrid")]
[ViewMap(typeof(RowDetailsSelectionDemo))]
public partial class RowDetailsSelectionDemoViewModel : ObservableObject
{
    private static readonly Random _random = new();
    private int _nextId = 1;

    public ObservableCollection<Employee> Employees { get; }
    public ObservableCollection<Employee> SelectedEmployees { get; } = [];

    [ObservableProperty] private Employee? _selectedEmployee;
    [ObservableProperty] private bool _areDetailsVisible = true;
    [ObservableProperty] private string _selectionInfo = "未选中";

    public RowDetailsSelectionDemoViewModel()
    {
        Employees = new ObservableCollection<Employee>(GenerateEmployees(35));
    }

    partial void OnSelectedEmployeeChanged(Employee? value)
    {
        UpdateSelectionInfo();
    }

    [RelayCommand]
    private void AddEmployee()
    {
        Employees.Add(CreateRandomEmployee());
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedEmployee is not null)
        {
            Employees.Remove(SelectedEmployee);
            SelectedEmployee = null;
        }
    }

    [RelayCommand]
    private void ResetData()
    {
        Employees.Clear();
        _nextId = 1;
        foreach (var emp in GenerateEmployees(35))
            Employees.Add(emp);
        SelectedEmployee = null;
    }

    [RelayCommand]
    private void ToggleDetailsVisibility()
    {
        AreDetailsVisible = !AreDetailsVisible;
    }

    [RelayCommand]
    private void Save()
    {
        ExportHelper.ExportToJson(Employees, "employees_details_export.json");
    }

    private void UpdateSelectionInfo()
    {
        SelectionInfo = SelectedEmployee is null
            ? "未选中"
            : $"已选中: {SelectedEmployee.FullName} ({SelectedEmployee.Department} - {SelectedEmployee.Position})";
    }

    private List<Employee> GenerateEmployees(int count)
    {
        var list = new List<Employee>(count);
        for (int i = 0; i < count; i++)
            list.Add(CreateRandomEmployee());
        return list;
    }

    private Employee CreateRandomEmployee()
    {
        var firstName = TestData.FirstNames[_random.Next(TestData.FirstNames.Length)];
        var lastName = TestData.LastNames[_random.Next(TestData.LastNames.Length)];
        var dept = TestData.Departments[_random.Next(TestData.Departments.Length)];
        var position = TestData.Positions[_random.Next(TestData.Positions.Length)];
        var city = TestData.Cities[_random.Next(TestData.Cities.Length)];
        var salary = TestData.GetSalaryForPosition(position, _random);
        var isActive = _random.NextDouble() > 0.15;
        var hireDate = DateTime.Now.AddDays(-_random.Next(30, 3650));
        var rating = TestData.GetRatingForPosition(position, _random);
        var email = $"{firstName.ToLower()}_{lastName.ToLower()}@company.com";
        var note = TestData.EmployeeNotes[_random.Next(TestData.EmployeeNotes.Length)];

        return new Employee(
            _nextId++, firstName, lastName, dept, position, salary,
            isActive, hireDate, rating, email, city, note
        );
    }
}

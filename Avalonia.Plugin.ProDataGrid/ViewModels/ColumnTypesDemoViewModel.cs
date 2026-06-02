using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.ProDataGrid.Models;
using Avalonia.Plugin.ProDataGrid.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Avalonia.Plugin.ProDataGrid.ViewModels;

[NavigationItem("KeyColumnTypes")]
[Menu("NAV_ColumnTypes", "KeyColumnTypes", "NAV_ProDataGrid")]
[ViewMap(typeof(ColumnTypesDemo))]
public partial class ColumnTypesDemoViewModel : ObservableObject
{
    private static readonly Random _random = new();
    private int _nextId = 1;

    public ObservableCollection<Employee> Employees { get; }

    public ObservableCollection<string> DepartmentOptions { get; } = new(TestData.Departments);

    [ObservableProperty] private Employee? _selectedEmployee;

    public ColumnTypesDemoViewModel()
    {
        Employees = new ObservableCollection<Employee>(GenerateEmployees(40));
    }

    [RelayCommand]
    private void AddEmployee()
    {
        Employees.Add(CreateRandomEmployee());
    }

    [RelayCommand]
    private void ResetData()
    {
        Employees.Clear();
        _nextId = 1;
        foreach (var emp in GenerateEmployees(40))
            Employees.Add(emp);
    }

    [RelayCommand]
    private void Save()
    {
        ExportHelper.ExportToJson(Employees, "employees_export.json");
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

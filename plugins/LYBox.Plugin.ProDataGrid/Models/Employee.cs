using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LYBox.Plugin.ProDataGrid.Models;

public partial class Employee : ObservableObject, IDataErrorInfo
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private string _firstName = string.Empty;
    [ObservableProperty] private string _lastName = string.Empty;
    [ObservableProperty] private string _department = string.Empty;
    [ObservableProperty] private string _position = string.Empty;
    [ObservableProperty] private double _salary;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private DateTime _hireDate;
    [ObservableProperty] private int _performanceRating;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _city = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;

    public Employee() { }

    public Employee(int id, string firstName, string lastName, string department,
        string position, double salary, bool isActive, DateTime hireDate,
        int performanceRating, string email, string city, string notes)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Department = department;
        Position = position;
        Salary = salary;
        IsActive = isActive;
        HireDate = hireDate;
        PerformanceRating = performanceRating;
        Email = email;
        City = city;
        Notes = notes;
    }

    /// <summary>
    /// 全名（计算属性，在 FirstName/LastName 变更时自动通知）。
    /// </summary>
    public string FullName => $"{FirstName} {LastName}";

    partial void OnFirstNameChanged(string value)
    {
        OnPropertyChanged(nameof(FullName));
    }

    partial void OnLastNameChanged(string value)
    {
        OnPropertyChanged(nameof(FullName));
    }

    // IDataErrorInfo 验证
    public string Error => string.Empty;

    public string this[string columnName] => columnName switch
    {
        nameof(FirstName) => string.IsNullOrWhiteSpace(FirstName) ? "名不能为空" : string.Empty,
        nameof(LastName) => string.IsNullOrWhiteSpace(LastName) ? "姓不能为空" : string.Empty,
        nameof(Department) => string.IsNullOrWhiteSpace(Department) ? "部门不能为空" : string.Empty,
        nameof(Position) => string.IsNullOrWhiteSpace(Position) ? "职位不能为空" : string.Empty,
        nameof(Salary) => Salary < 0 ? "薪资不能为负数" : string.Empty,
        nameof(Email) => !string.IsNullOrWhiteSpace(Email) && !Email.Contains('@') ? "邮箱格式不正确" : string.Empty,
        nameof(PerformanceRating) => PerformanceRating is < 1 or > 5 ? "评分应在 1-5 之间" : string.Empty,
        _ => string.Empty
    };
}

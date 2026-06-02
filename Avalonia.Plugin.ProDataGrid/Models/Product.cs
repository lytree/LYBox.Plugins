using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.ProDataGrid.Models;

public partial class Product : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private double _price;
    [ObservableProperty] private int _quantity;

    public Product() { }

    public Product(string name, double price, int quantity)
    {
        Name = name;
        Price = price;
        Quantity = quantity;
    }

    /// <summary>
    /// 总价 = 单价 × 数量（计算属性，无冗余状态）。
    /// </summary>
    public double Total => Price * Quantity;

    partial void OnPriceChanged(double value)
    {
        OnPropertyChanged(nameof(Total));
    }

    partial void OnQuantityChanged(int value)
    {
        OnPropertyChanged(nameof(Total));
    }
}

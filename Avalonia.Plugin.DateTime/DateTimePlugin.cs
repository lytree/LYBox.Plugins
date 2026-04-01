using Avalonia.Plugin.Shared;
using Avalonia.Plugin.DateTimeControls.ViewModels;
using Avalonia.Plugin.Shared.ViewModels;

namespace Avalonia.Plugin.DateTimeControls;

public  partial  class DateTimePlugin : IPluginMetadata
{
    public string Name => "Date & Time Plugin";
    public string Version => "1.0.0";

    public string Author => throw new NotImplementedException();

    public string Description => throw new NotImplementedException();

    public IEnumerable<string> Dependencies => throw new NotImplementedException();

    public string PluginId => throw new NotImplementedException();

    public void Initialize()
    {
        // 插件初始化逻辑
    }

    public Dictionary<string, ViewModelFactory> GetNavigationItems()
    {
        var navigationItems = new Dictionary<string, ViewModelFactory>
        {
            { "KeyDatePicker", () => new DatePickerDemoViewModel() },
            { "KeyDateRangePicker", () => new DateRangePickerDemoViewModel() },
            { "KeyDateTimePicker", () => new DateTimePickerDemoViewModel() },
            { "KeyTimeBox", () => new TimeBoxDemoViewModel() },
            { "KeyTimePicker", () => new TimePickerDemoViewModel() },
            { "KeyTimeRangePicker", () => new TimeRangePickerDemoViewModel() },
            { "KeyClock", () => new ClockDemoViewModel() }
        };

        return navigationItems;
    }

    public IEnumerable<(string? ParentKey, MenuItemViewModel MenuItem)> GetMenuItems()
    {
        var menuItems = new List<(string? ParentKey, MenuItemViewModel MenuItem)>();

        var dateAndTime = new MenuItemViewModel
        {
            MenuHeader = "Date & Time",
            Children = new()
            {
                new() { MenuHeader = "Date Picker", Key = "KeyDatePicker" },
                new() { MenuHeader = "Date Range Picker", Key = "KeyDateRangePicker" },
                new() { MenuHeader = "Date Time Picker", Key = "KeyDateTimePicker" },
                new() { MenuHeader = "Time Box", Key = "KeyTimeBox" },
                new() { MenuHeader = "Time Picker", Key = "KeyTimePicker" },
                new() { MenuHeader = "Time Range Picker", Key = "KeyTimeRangePicker" },
                new() { MenuHeader = "Clock", Key = "KeyClock" }
            }
        };
        menuItems.Add((null, dateAndTime)); ;

        return menuItems;
    }


}




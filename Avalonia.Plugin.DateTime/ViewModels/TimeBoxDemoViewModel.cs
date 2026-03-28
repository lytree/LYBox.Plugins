using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avalonia.Plugin.DateTimeControls.ViewModels;


public partial class TimeBoxDemoViewModel : ObservableObject
{
    [ObservableProperty] private TimeSpan? _timeSpan;

    [RelayCommand]
    private void ChangeRandomTime()
    {
        TimeSpan = new TimeSpan(Random.Shared.NextInt64(0x00000000FFFFFFFF));
    }
    
    public TimeBoxDemoViewModel()
    {
        TimeSpan = new TimeSpan(0, 21, 11, 36, 54);
    }
}






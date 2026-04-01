using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.ButtonsInputs.Pages;
using System.Collections.ObjectModel;

namespace Avalonia.Plugin.ButtonsInputs.ViewModels;

[NavigationItem("TagInput")]
[Menu("TagInput", "TagInput", "ButtonsInputs")]
[ViewMap(typeof(TagInputDemo))]
public class TagInputDemoViewModel: ViewModelBase
{
    private ObservableCollection<string> _tags = new () ;
    public ObservableCollection<string> Tags
    {
        get => _tags;
        set => SetProperty(ref _tags, value);
    }

    private ObservableCollection<string> _distinctTags = new();
    public ObservableCollection<string> DistinctTags
    {
        get => _distinctTags;
        set => SetProperty(ref _distinctTags, value);
    }
}






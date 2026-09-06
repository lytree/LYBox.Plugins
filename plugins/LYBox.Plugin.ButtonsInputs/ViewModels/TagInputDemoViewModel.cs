using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Attributes;
using LYBox.Plugin.ButtonsInputs.Pages;
using System.Collections.ObjectModel;

namespace LYBox.Plugin.ButtonsInputs.ViewModels;

[NavigationItem("TagInput")]
[Menu("NAV_TagInput", "TagInput", "NAV_ButtonsInputs")]
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






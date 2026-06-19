using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Avalonia.Plugin.TDLSharp.Pages;

public partial class LoginPage : UserControl
{
    public LoginPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}

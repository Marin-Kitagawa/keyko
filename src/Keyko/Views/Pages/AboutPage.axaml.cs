
using System.Reflection;
using Avalonia.Controls;

namespace Keyko.Views.Pages;

public partial class AboutPage : UserControl
{
    public AboutPage()
    {
        InitializeComponent();

        if (this.FindControl<TextBlock>("VersionText") is { } version && typeof(AboutPage).Assembly.GetName().Version is { } v)
        {
            version.Text = $"Version {v.Major}.{v.Minor}.{v.Build} · Avalonia UI · .NET 10";
        }
    }
}

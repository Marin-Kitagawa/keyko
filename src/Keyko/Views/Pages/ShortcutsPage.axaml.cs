using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Keyko.ViewModels;

namespace Keyko.Views.Pages;

public partial class ShortcutsPage : UserControl
{
    public ShortcutsPage()
    {
        InitializeComponent();
    }

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border { DataContext: ShortcutItemViewModel item } &&
            DataContext is MainViewModel vm)
        {
            _ = vm.EditAsync(item);
        }
    }
}

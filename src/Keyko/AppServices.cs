using Avalonia.Controls;
using Keyko.Services;
using Keyko.Views;

namespace Keyko;

public static class AppServices
{
    public static ConfigService Config { get; private set; } = null!;
    public static HotkeyService Hotkeys { get; private set; } = null!;
    public static ExpansionService Expansion { get; private set; } = new();
    public static MainWindow? MainWindow { get; set; }

    public static void Init()
    {
        Config = new ConfigService();
        Config.Load();
        Config.Migrate();
        UiTheme.Init(Avalonia.Application.Current!);
        UiTheme.Apply(Avalonia.Application.Current!, Config.Settings.Theme, Config.Settings.Accent1, Config.Settings.Accent2);
        Hotkeys = new HotkeyService();
    }
}

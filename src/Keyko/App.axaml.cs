using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Keyko.Services;
using Keyko.Selftest;
using Keyko.ViewModels;
using Keyko.Views;

namespace Keyko;

public partial class App : Application
{
    public static bool IsSelftest { get; private set; }
    public static string[] CliArgs { get; private set; } = Array.Empty<string>();
    public static MainViewModel? Vm { get; private set; }
    private TrayIcon? _tray;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        CliArgs = (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Args ?? Array.Empty<string>();
        IsSelftest = CliArgs.Contains("--selftest");

        AppServices.Init();

        // construct the script/display font with the DIRECT ctor — proven to resolve its
        // glyph typeface (the XAML-parsed FontFamily element with identical values does not)

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // avoid duplicate DataContext from XAML
            Vm = new MainViewModel(AppServices.Config);

            desktop.MainWindow = new MainWindow { DataContext = Vm };
            AppServices.MainWindow = (MainWindow)desktop.MainWindow;

            if (!IsSelftest)
            {
                // registration happens on the hotkey thread; the count comes back via Armed
                AppServices.Hotkeys.Armed += armed => ToastService.Show(
                    armed > 0 ? $"♡ {armed} hotkey" + (armed == 1 ? "" : "s") + " armed" : "No hotkeys armed",
                    armed > 0
                        ? "They work system-wide — even from the tray."
                        : "Check the conflict banner in Keyko for combos other apps already own.",
                    glyph: armed > 0 ? "\uE73E" : "\uE783",
                    settings: null);

                Vm.RegisterHotkeys();

                if (AppServices.Config.Settings.LaunchOnStartup)
                    AutostartService.Apply(true, AppServices.Config.Settings.RunAsAdmin);

                if (AppServices.Config.Settings.RunAsAdmin && !Elevation.IsElevated())
                {
                    ToastService.Show("Running without admin rights",
                        "Hotkeys won't reach elevated windows until Keyko is restarted as admin.",
                        "🛡️",
                        settings: null,
                        action: ("Restart as admin", () =>
                        {
                            if (Elevation.TryRelaunch(asAdmin: true)) ExitApp();
                        }));
                }
            }

            SetupTrayIcon();

            if (AppServices.Config.Settings.LaunchOnStartup != AutostartService.IsEnabled())
            {
                AppServices.Config.Settings.LaunchOnStartup = AutostartService.IsEnabled();
            }

            if (IsSelftest)
            {
                SelftestRunner.Begin(AppServices.MainWindow!);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon()
    {
        try
        {
            WindowIcon icon;
            using (var s = AssetLoader.Open(new Uri("avares://Keyko/Assets/tray.png")))
                icon = new WindowIcon(new Bitmap(s));

            _tray = new TrayIcon
            {
                Icon = icon,
                ToolTipText = "Keyko — your cozy hotkey friend",
                IsVisible = true,
            };
            _tray.Clicked += (_, _) => ShowMainWindow();
            _tray.Menu = BuildTrayMenu();
            var icons = new TrayIcons { _tray };
            TrayIcon.SetIcons(this, icons);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Keyko: tray icon failed. " + ex.Message);
        }
    }

    private NativeMenu BuildTrayMenu()
    {
        var menu = new NativeMenu();

        var show = new NativeMenuItem("Open Keyko");
        show.Click += (_, _) => ShowMainWindow();
        menu.Add(show);

        if (!Elevation.IsElevated())
        {
            var admin = new NativeMenuItem("Restart as administrator");
            admin.Click += (_, _) =>
            {
                if (Elevation.TryRelaunch(asAdmin: true)) ExitApp();
            };
            menu.Add(admin);
        }

        menu.Add(new NativeMenuItemSeparator());

        foreach (var item in Vm?.Items.Where(i => i.IsEnabled && i.HasHotkey).Take(6) ?? Enumerable.Empty<ShortcutItemViewModel>())
        {
            var captured = item.Model;
            var mi = new NativeMenuItem($"{item.Model.TileGlyph}  {item.Name}  ({item.HotkeyDisplay})");
            mi.Click += (_, _) => _ = Vm?.ExecuteAsync(captured, manual: false);
            menu.Add(mi);
        }

        menu.Add(new NativeMenuItemSeparator());
        var exit = new NativeMenuItem("Exit");
        exit.Click += (_, _) => ExitApp();
        menu.Add(exit);
        return menu;
    }
    public static void ShowMainWindow()
    {
        var w = AppServices.MainWindow;
        if (w is null) return;
        w.BringToFrontFromTray();
    }

    public static void ExitApp()
    {
        if (AppServices.MainWindow is { } w)
            w.CloseForReal();
        else if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
            d.Shutdown();
    }
}

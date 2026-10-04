using System;
using System.Linq;
using Avalonia;
using Avalonia.Threading;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Keyko.Services;
using Keyko.Interop;
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
                AppServices.Hotkeys.Armed += armed =>
                {
                    ToastService.Show(
                        armed > 0 ? $"♡ {armed} hotkey" + (armed == 1 ? "" : "s") + " armed" : "No hotkeys armed",
                        armed > 0
                            ? "They work system-wide — even from the tray."
                            : "Check the conflict banner in Keyko for combos other apps already own.",
                        glyph: armed > 0 ? "\uE73E" : "\uE783",
                        settings: null);
                    if (AppServices.Config.Settings.SoundOnLaunch && armed > 0)
                        try { Keyko.Interop.Native.MessageBeep(0xFFFFFFFFu); } catch { }
                };

                // foreground tracking (per-app scoping, exclusions, insights)
                ForegroundMonitor.Start();

                // system hotkeys: search overlay, pause, profile cycling
                AppServices.Hotkeys.SystemHotkey += id => HandleSystemHotkey(id);
                AppServices.Hotkeys.SystemHotkeyFailed += (id, hk) => ToastService.Show(
                    "Combo already taken", $"{hk} couldn't be armed for {id}.", "\uE783", settings: null);
                ApplySystemHotkeys();

                Vm.RegisterHotkeys();

                // scheduled shortcuts
                SchedulerService.Start(a => _ = Vm.ExecuteAsync(a, manual: false));
                SchedulerService.SetScheduled(AppServices.Config.Settings.Shortcuts);

                // text expansion hook
                AppServices.Expansion.SetExpansions(AppServices.Config.Settings.Shortcuts);
                AppServices.Expansion.Start();

                // CLI companion pipe
                CliService.StartServer(HandleCli);

                // follow-system theme poller
                StartThemeFollow();

                if (AppServices.Config.Settings.LaunchOnStartup)
                    AutostartService.Apply(true, AppServices.Config.Settings.RunAsAdmin);

                if (AppServices.Config.Settings.RunAsAdmin && !Elevation.IsElevated())
                {
                    ToastService.Show("Running without admin rights",
                        "Hotkeys won't reach elevated windows until Keyko is restarted as admin.",
                        "\uE72E",
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
            var mi = new NativeMenuItem($"{item.Model.Name}  ({item.HotkeyDisplay})");
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

public partial class App
{
    // ---- system hotkeys (search overlay / pause / profile cycle) ----
    private static SearchOverlayWindow? _searchOverlay;

    public static void HandleSystemHotkey(string id)
    {
        var cfg = AppServices.Config.Settings;
        switch (id)
        {
            case "search":
                var main = AppServices.MainWindow;
                if (main is null) break;
                if (App._searchOverlay is { IsVisible: true }) App._searchOverlay.Close();
                App._searchOverlay = new SearchOverlayWindow(main);
                App._searchOverlay.Show();
                break;
            case "pause":
                KeykoState.TogglePause();
                ToastService.Show(
                    KeykoState.Paused ? "Keyko paused" : "Keyko resumed",
                    KeykoState.Paused ? "No hotkeys fire until you resume." : "All hotkeys are live again.",
                    KeykoState.Paused ? "\uE769" : "\uE73E",
                    settings: null);
                break;
            case "profile":
                Vm?.CycleProfile();
                break;
        }
    }

    private static void ApplySystemHotkeys()
    {
        var s = AppServices.Config.Settings;
        AppServices.Hotkeys.ApplySystem(new (string, string)[]
        {
            ("search", s.SearchHotkey ?? ""),
            ("pause", s.PauseHotkey ?? ""),
            ("profile", s.ProfileCycleHotkey ?? ""),
        });
    }

    public static void ReapplySystemHotkeys() => ApplySystemHotkeys();

    private static DispatcherTimer? _themeTimer;
    private static int _lastAppsUseLight = -1;

    private void StartThemeFollow()
    {
        if (_themeTimer is not null) return;
        _themeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _themeTimer.Tick += (_, _) =>
        {
            var cfg = AppServices.Config;
            if (!cfg.Settings.FollowSystemTheme || IsSelftest) return;
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var light = Convert.ToInt32(key?.GetValue("AppsUseLightTheme", 1));
                if (light == _lastAppsUseLight) return;
                _lastAppsUseLight = light;
                var theme = light == 1 ? "Light" : "Dark";
                if (!string.Equals(cfg.Settings.Theme, theme, StringComparison.OrdinalIgnoreCase))
                {
                    cfg.Settings.Theme = theme;
                    cfg.Save();
                    UiTheme.Apply(Application.Current!, theme, cfg.Settings.Accent1, cfg.Settings.Accent2);
                    AppServices.MainWindow?.ShowPage(cfg.Settings.Theme == "Dark" ? NavPage.Shortcuts : NavPage.Settings);
                }
            }
            catch { }
        };
        _themeTimer.Start();
    }

    public string HandleCli(string command)
    {
        var parts = command.Split(' ', 2, StringSplitOptions.TrimEntries);
        var cfg = AppServices.Config;
        var verb = parts[0].ToLowerInvariant();
        var arg = parts.Length > 1 ? parts[1] : "";
        return verb switch
        {
            "list" => string.Join("\n", Vm?.Items.Select(i => $"{i.Name} — {i.HotkeyDisplay}") ?? Array.Empty<string>()),
            "run" => RunByName(arg),
            "pause" => DoPause(true),
            "resume" => DoPause(false),
            "toggle" => DoPause(!KeykoState.Paused),
            "profile" => Vm?.SwitchProfileByName(arg) is true ? "profile: " + arg : "profile not found: " + arg,
            "version" => "Keyko " + typeof(App).Assembly.GetName().Version?.ToString(3),
            _ => "unknown command: " + verb,
        };

        string RunByName(string name)
        {
            var item = Vm?.Items.FirstOrDefault(i =>
                string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));
            if (item is null) return "no shortcut named: " + name;
            _ = Vm!.ExecuteAsync(item.Model, manual: true);
            return "launched: " + item.Name;
        }

        string DoPause(bool paused)
        {
            KeykoState.SetPaused(paused);
            return paused ? "paused" : "resumed";
        }
    }
}

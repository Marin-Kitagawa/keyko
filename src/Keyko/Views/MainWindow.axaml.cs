using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Interactivity;
using Keyko.Services;
using Keyko.ViewModels;
using Keyko.Views.Pages;

namespace Keyko.Views;

public partial class MainWindow : Window
{
    private bool _allowClose;
    private MainViewModel? _vm;

    public MainWindow()
    {
        InitializeComponent();

        if (App.IsSelftest)
            ShowActivated = false;

        _vm = DataContext as MainViewModel;
        DataContextChanged += (_, _) => { _vm = DataContext as MainViewModel; WireVm(); };

        Loaded += OnLoaded;
        Closing += OnClosing;
        PropertyChanged += OnSelfPropertyChanged;

        DragZone.PointerPressed += OnDragZonePressed;
        ThemeToggle.Click += (_, _) => ToggleTheme();
        OpenSettings.Click += (_, _) => _vm?.SetPage(NavPage.Settings);
    }

    private void WireVm()
    {
        if (_vm is not null)
        {
            _vm.PageChanged -= ShowPage;
            _vm.PageChanged += ShowPage;
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        GlassService.ApplyTo(Acrylic.Material!, AppServices.Config.Settings);
        GlassService.Changed += OnGlassChanged;

        if (this.FindControl<Control>("AdminBadge") is { } badge)
            badge.IsVisible = Elevation.IsElevated();

        if (!App.IsSelftest)
        {
            ShowPage(_vm?.CurrentPage ?? NavPage.Shortcuts);
            if (AppServices.Config.Settings.StartMinimized && App.CliArgs.Contains("--minimized"))
                Hide();
        }
        else
        {
            BaseWallpaper.IsVisible = true;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint(60, 60);
            ShowPage(_vm?.CurrentPage ?? NavPage.Shortcuts);

            // font diagnostics (selftest only)
            foreach (var (name, family) in new[]
            {
                ("FiraCode-embedded", new FontFamily("avares://Keyko/Assets/Fonts/FiraCode.ttf#Fira Code")),
                ("Lavishly-embedded", new FontFamily("avares://Keyko/Assets/Fonts/LavishlyYours-Regular.ttf#Lavishly Yours")),
                ("Playfair-embedded", new FontFamily("avares://Keyko/Assets/Fonts/PlayfairDisplay.ttf#Playfair Display")),
                ("FiraCode-filepath", new FontFamily(new Uri(AppContext.BaseDirectory), "FiraCodeNerdFont-Regular.ttf#Fira Code Nerd Font")),
                ("Lavishly-filepath", new FontFamily(new Uri(AppContext.BaseDirectory), "LavishlyYours-Regular.ttf#Lavishly Yours")),
            })
            {
                try
                {
                    var ok = FontManager.Current.TryGetGlyphTypeface(new Typeface(family), out var gt);
                    Console.WriteLine($"DIAG font {name}: glyph={(ok ? gt!.FamilyName : "FAIL")}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"DIAG font {name}: EX {ex.Message}");
                }
            }
        }
    }

    private void OnGlassChanged()
    {
        try { GlassService.ApplyTo(Acrylic.Material!, AppServices.Config.Settings); }
        catch { /* window may be closing */ }
    }

    protected override void OnClosed(EventArgs e)
    {
        GlassService.Changed -= OnGlassChanged;
        base.OnClosed(e);
    }

    private void OnSelfPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowStateProperty)
        {
            RootGrid.Margin = WindowState == WindowState.Maximized ? new Thickness(8) : default;
        }
    }

    public void ShowPage(NavPage page)
    {
        var vm = _vm ?? DataContext as MainViewModel;
        if (vm is null) return;
        Pages.Content = page switch
        {
            NavPage.Settings => new SettingsPage { DataContext = vm.SettingsVM },
            NavPage.Insights => new InsightsPage { DataContext = vm.InsightsVM },
            NavPage.About => new AboutPage(),
            _ => new ShortcutsPage { DataContext = vm },
        };
        var glyph = this.FindControl<TextBlock>("ThemeToggleGlyph");
        if (glyph is not null)
            glyph.Text = UiTheme.IsDark ? "\uE706" : "\uE708"; // sun when dark, moon when light
    }

    private void ToggleTheme()
    {
        var cfg = AppServices.Config;
        cfg.Settings.Theme = UiTheme.IsDark ? "Light" : "Dark";
        UiTheme.Apply(Application.Current!, cfg.Settings.Theme, cfg.Settings.Accent1, cfg.Settings.Accent2);
        cfg.Save();
        ShowPage(_vm?.CurrentPage ?? NavPage.Shortcuts);
    }

    private void OnDragZonePressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
            else
            {
                try { BeginMoveDrag(e); } catch { }
            }
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        Hide();
    }

    public void CloseForReal()
    {
        _allowClose = true;
        Close();
    }

    public void BringToFrontFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
    }
}

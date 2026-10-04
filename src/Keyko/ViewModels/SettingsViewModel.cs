using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Keyko.Models;
using Keyko.Services;

namespace Keyko.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ConfigService _config;
    private readonly MainViewModel _main;

    public SettingsViewModel(ConfigService config, MainViewModel main)
    {
        _config = config;
        _main = main;
        var s = config.Settings;

        _glassOpacity = s.GlassTintOpacity;
        _startMinimized = s.StartMinimized;
        _showToasts = s.ShowToasts;
        _launchOnStartup = AutostartService.IsEnabled();
        _runAsAdmin = s.RunAsAdmin;
        _followSystemTheme = s.FollowSystemTheme;
        _soundOnLaunch = s.SoundOnLaunch;
        _searchHotkey = s.SearchHotkey ?? "";
        _pauseHotkey = s.PauseHotkey ?? "";
        _profileCycleHotkey = s.ProfileCycleHotkey ?? "";
        _globalExclusions = string.Join("; ", s.GlobalAppExclusions);
        _profiles = new ObservableCollection<string>(s.Profiles.Select(p => p.Name));
        _selectedProfile = s.ActiveProfile;

        foreach (var a in UiTheme.Accents)
        {
            var preset = new AccentPresetViewModel(a.Name, a.C1, a.C2);
            preset.IsSelected = string.Equals(a.C1, s.Accent1, StringComparison.OrdinalIgnoreCase);
            AccentPresets.Add(preset);
        }

        if (AccentPresets.All(p => !p.IsSelected) && AccentPresets.Count > 0)
            AccentPresets[0].IsSelected = true;
    }

    public string ConfigPath => _config.FilePath;

    public bool IsDarkTheme => !string.Equals(_config.Settings.Theme, "Light", StringComparison.OrdinalIgnoreCase);

    public bool IsLightTheme => !IsDarkTheme;

    [ObservableProperty]
    private double _glassOpacity;

    [ObservableProperty]
    private bool _startMinimized;

    [ObservableProperty]
    private bool _showToasts;

    [ObservableProperty]
    private bool _launchOnStartup;

    [ObservableProperty]
    private bool _runAsAdmin;

    public bool IsElevated => Elevation.IsElevated();

    public string ElevationStatus => IsElevated
        ? "Keyko is running with administrator rights — hotkeys reach elevated windows."
        : "Running without admin rights — hotkeys won't reach elevated (admin) windows.";

    public ObservableCollection<AccentPresetViewModel> AccentPresets { get; } = new();

    // ---- power-user settings ----
    [ObservableProperty] private bool _followSystemTheme;
    [ObservableProperty] private bool _soundOnLaunch;
    [ObservableProperty] private string _searchHotkey;
    [ObservableProperty] private string _pauseHotkey;
    [ObservableProperty] private string _profileCycleHotkey;
    [ObservableProperty] private string _globalExclusions;
    [ObservableProperty] private string _selectedProfile;
    [ObservableProperty] private System.Collections.ObjectModel.ObservableCollection<string> _profiles;
    [ObservableProperty] private string _newProfileName = "";

    public void SetHotkeyField(string which, HotkeyGesture? g)
    {
        var text = g?.ToString() ?? "";
        switch (which)
        {
            case "search": SearchHotkey = text; break;
            case "pause": PauseHotkey = text; break;
            case "profile": ProfileCycleHotkey = text; break;
        }
    }

    partial void OnFollowSystemThemeChanged(bool value)
    {
        _config.Settings.FollowSystemTheme = value;
        _config.Save();
    }

    partial void OnSoundOnLaunchChanged(bool value)
    {
        _config.Settings.SoundOnLaunch = value;
        _config.Save();
    }

    partial void OnSearchHotkeyChanged(string value)
    {
        _config.Settings.SearchHotkey = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        _config.Save();
        App.ReapplySystemHotkeys();
    }

    partial void OnPauseHotkeyChanged(string value)
    {
        _config.Settings.PauseHotkey = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        _config.Save();
        App.ReapplySystemHotkeys();
    }

    partial void OnProfileCycleHotkeyChanged(string value)
    {
        _config.Settings.ProfileCycleHotkey = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        _config.Save();
        App.ReapplySystemHotkeys();
    }

    partial void OnGlobalExclusionsChanged(string value)
    {
        _config.Settings.GlobalAppExclusions = (value ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        _config.Save();
        KeykoState.SetExclusions(_config.Settings.GlobalAppExclusions);
    }

    [RelayCommand]
    private void CreateProfile()
    {
        var name = NewProfileName.Trim();
        if (name.Length == 0 || _config.Settings.Profiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            return;
        _config.Settings.Profiles.Add(new ProfileSet { Name = name, Shortcuts = new() });
        _config.Save();
        Profiles.Add(name);
        NewProfileName = "";
    }

    [RelayCommand]
    private void SwitchProfile(string? name)
    {
        var target = _config.Settings.Profiles.FirstOrDefault(
            p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (target is null) return;
        _main.SwitchProfileTo(target);
        SelectedProfile = target.Name;
    }

    partial void OnGlassOpacityChanged(double value)
    {
        _config.Settings.GlassTintOpacity = value;
        GlassService.NotifyChanged();
        _config.Save();
    }

    partial void OnStartMinimizedChanged(bool value)
    {
        _config.Settings.StartMinimized = value;
        _config.Save();
    }

    partial void OnShowToastsChanged(bool value)
    {
        _config.Settings.ShowToasts = value;
        _config.Save();
    }

    partial void OnLaunchOnStartupChanged(bool value)
    {
        AutostartService.Apply(value, _config.Settings.RunAsAdmin);
        _config.Settings.LaunchOnStartup = value;
        _config.Save();
    }

    partial void OnRunAsAdminChanged(bool value)
    {
        _config.Settings.RunAsAdmin = value;
        _config.Save();

        if (value == Elevation.IsElevated())
        {
            // nothing to relaunch; just keep autostart in sync with the new mode
            if (_config.Settings.LaunchOnStartup)
                AutostartService.Apply(true, value);
            return;
        }

        if (Elevation.TryRelaunch(asAdmin: value))
        {
            App.ExitApp();
        }
        else
        {
            // user cancelled the UAC prompt (or launch failed) — revert
            _config.Settings.RunAsAdmin = !value;
            _config.Save();
            RunAsAdmin = !value; // triggers this handler again, which early-returns
            ToastService.Show("Restart cancelled", "Administrator rights were not granted.", "⚠️", settings: null);
        }
    }

    [RelayCommand]
    private void SetTheme(string theme)
    {
        _config.Settings.Theme = theme;
        UiTheme.Apply(Avalonia.Application.Current!, theme, _config.Settings.Accent1, _config.Settings.Accent2);
        _config.Save();
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(IsLightTheme));
        foreach (var item in _main.Items) item.Refresh();
    }

    [RelayCommand]
    private void SetAccent(string name)
    {
        var preset = AccentPresets.FirstOrDefault(p => p.Name == name);
        if (preset is null) return;
        foreach (var p in AccentPresets) p.IsSelected = p == preset;
        _config.Settings.Accent1 = preset.C1;
        _config.Settings.Accent2 = preset.C2;
        UiTheme.Apply(Avalonia.Application.Current!, _config.Settings.Theme, preset.C1, preset.C2);
        _config.Save();
    }

    [RelayCommand]
    private void OpenConfigFolder()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_config.Dir) { UseShellExecute = true });
        }
        catch { }
    }
}

public partial class AccentPresetViewModel : ObservableObject
{
    public string Name { get; }
    public string C1 { get; }
    public string C2 { get; }

    [ObservableProperty]
    private bool _isSelected;

    public AccentPresetViewModel(string name, string c1, string c2)
    {
        Name = name;
        C1 = c1;
        C2 = c2;
    }

    public IBrush Swatch => new LinearGradientBrush
    {
        StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
        EndPoint = new Avalonia.RelativePoint(1, 1, Avalonia.RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.Parse(C1), 0),
            new GradientStop(Color.Parse(C2), 1),
        },
    };
}

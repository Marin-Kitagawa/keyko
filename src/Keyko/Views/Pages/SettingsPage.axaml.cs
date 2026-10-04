using Avalonia.Controls;
using Avalonia.Interactivity;
using Keyko.Models;
using Keyko.ViewModels;

namespace Keyko.Views.Pages;

public partial class SettingsPage : UserControl
{
    private HotkeyRecorderBox? _search;
    private HotkeyRecorderBox? _pause;
    private HotkeyRecorderBox? _profile;

    public SettingsPage()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => WireRecorders();
        DetachedFromVisualTree += (_, _) => UnwireRecorders();
    }

    private void WireRecorders()
    {
        if (DataContext is not SettingsViewModel vm) return;

        _search = this.FindControl<HotkeyRecorderBox>("SearchRecorder");
        _pause = this.FindControl<HotkeyRecorderBox>("PauseRecorder");
        _profile = this.FindControl<HotkeyRecorderBox>("ProfileRecorder");

        if (_search is { } s)
        {
            s.SetGesture(HotkeyGesture.TryParse(vm.SearchHotkey, out var sg) ? sg : null);
            s.HotkeyChanged += g => vm.SetHotkeyField("search", g);
            s.Cleared += () => vm.SetHotkeyField("search", null!);
        }
        if (_pause is { } p)
        {
            p.SetGesture(HotkeyGesture.TryParse(vm.PauseHotkey, out var pg) ? pg : null);
            p.HotkeyChanged += g => vm.SetHotkeyField("pause", g);
            p.Cleared += () => vm.SetHotkeyField("pause", null!);
        }
        if (_profile is { } pr)
        {
            pr.SetGesture(HotkeyGesture.TryParse(vm.ProfileCycleHotkey, out var cg) ? cg : null);
            pr.HotkeyChanged += g => vm.SetHotkeyField("profile", g);
            pr.Cleared += () => vm.SetHotkeyField("profile", null!);
        }
    }

    private void UnwireRecorders()
    {
        _search?.Unwire();
        _pause?.Unwire();
        _profile?.Unwire();
    }
}

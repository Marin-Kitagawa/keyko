using System;
using Avalonia.Threading;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Keyko.Models;
using Keyko.Services;

namespace Keyko.ViewModels;

/// <summary>
/// Insights page: usage bars, the hotkey doctor (armed/failed/conflicts) and the
/// clipboard ring, with CSV export.
/// </summary>
public partial class InsightsViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private readonly ConfigService _config;

    public InsightsViewModel(MainViewModel main, ConfigService config)
    {
        _main = main;
        _config = config;
        Refresh();
        ClipboardRing.Added += _ => OnRingChanged();
    }

    public partial class UsageRow : ObservableObject
    {
        public string Name { get; init; } = "";
        public string Hotkey { get; init; } = "";
        public int Runs { get; init; }
        public double BarWidth { get; init; }
        public IBrush BarBrush { get; init; } = Brushes.Transparent;
    }

    public partial class DoctorRow : ObservableObject
    {
        public string Name { get; init; } = "";
        public string Hotkey { get; init; } = "";
        public string State { get; init; } = "";
        public string Note { get; init; } = "";
        public IBrush StateBrush { get; init; } = Brushes.Transparent;
    }

    public partial class RingRow : ObservableObject
    {
        public string Text { get; init; } = "";
        public string Preview { get; init; } = "";
    }

    [ObservableProperty] private System.Collections.ObjectModel.ObservableCollection<UsageRow> _usage = new();
    [ObservableProperty] private System.Collections.ObjectModel.ObservableCollection<DoctorRow> _doctor = new();
    [ObservableProperty] private System.Collections.ObjectModel.ObservableCollection<RingRow> _ring = new();
    [ObservableProperty] private int _totalLaunches;
    [ObservableProperty] private int _armedCount;
    [ObservableProperty] private string _pausedText = "";

    public IBrush StateArmed => UiTheme.Brush("Success");
    public IBrush StateFailed => UiTheme.Brush("Danger");

    public void Refresh()
    {
        var items = _main.Items.Where(i => i.HasHotkey).OrderByDescending(i => i.Model.RunCount).ToList();
        var max = Math.Max(1, items.Max(i => i.Model.RunCount));

        Usage.Clear();
        foreach (var i in items)
        {
            Usage.Add(new UsageRow
            {
                Name = i.Name,
                Hotkey = i.HotkeyDisplay,
                Runs = i.Model.RunCount,
                BarWidth = 40 + 260.0 * i.Model.RunCount / max,
                BarBrush = new LinearGradientBrush
                {
                    StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
                    EndPoint = new Avalonia.RelativePoint(1, 0, Avalonia.RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.Parse("#F472B6"), 0),
                        new GradientStop(Color.Parse("#A78BFA"), 1),
                    },
                },
            });
        }

        // doctor: armed = registered, failed = conflict list, disabled = skipped
        Doctor.Clear();
        var failedNames = new HashSet<string>(
            (_main.ConflictShortcuts ?? Enumerable.Empty<ShortcutAction>())
                .Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var i in _main.Items)
        {
            string state, note;
            IBrush brush;
            if (failedNames.Contains(i.Name))
            {
                state = "failed"; note = "combo taken by another app"; brush = UiTheme.Brush("Danger");
            }
            else if (!i.IsEnabled)
            {
                state = "disarmed"; note = "toggle is off"; brush = UiTheme.Brush("TextTertiary");
            }
            else if (!i.HasHotkey)
            {
                state = "no hotkey"; note = "record a combo to arm it"; brush = UiTheme.Brush("Warning");
            }
            else
            {
                state = "armed"; note = KeykoState.Paused ? "paused" : "live"; brush = UiTheme.Brush("Success");
            }
            Doctor.Add(new DoctorRow
            {
                Name = i.Name,
                Hotkey = i.HotkeyDisplay,
                State = state,
                Note = note,
                StateBrush = brush,
            });
        }

        Ring.Clear();
        foreach (var t in ClipboardRing.Items)
            Ring.Add(new RingRow
            {
                Text = t,
                Preview = t.Length > 90 ? t[..90] + "…" : t,
            });

        TotalLaunches = items.Sum(i => i.Model.RunCount);
        ArmedCount = items.Count(i => i.IsEnabled);
        PausedText = KeykoState.Paused ? "Keyko is paused — no hotkeys fire" : "";
    }

    private void OnRingChanged() => Dispatcher.UIThread.Post(Refresh);

    [RelayCommand]
    private void PasteRingItem(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        ClipboardText.SetText(text);
        ToastService.Show("Ready to paste", "Press Ctrl+V in the target app.", "\uE77F", settings: _config.Settings);
    }

    [RelayCommand]
    private void ClearRing()
    {
        ClipboardRing.Clear();
        Refresh();
    }

    [RelayCommand]
    private void TogglePause()
    {
        KeykoState.TogglePause();
        Refresh();
    }

    [RelayCommand]
    private void ExportCsv()
    {
        try
        {
            var sb = new StringBuilder("name,hotkey,runs,last_used\n");
            foreach (var i in _main.Items)
                sb.Append('"').Append(i.Name.Replace("\"", "\"\"")).Append("\",")
                  .Append(i.HotkeyDisplay.Replace(',', ';')).Append(',')
                  .Append(i.Model.RunCount).Append(',')
                  .Append(i.Model.LastUsedAt?.ToString("yyyy-MM-dd HH:mm") ?? "").Append('\n');
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var path = Path.Combine(dir, $"keyko-usage-{DateTime.Now:yyyyMMdd-HHmm}.csv");
            File.WriteAllText(path, sb.ToString());
            ToastService.Show("Usage exported", path, "\uE896", settings: _config.Settings);
        }
        catch (Exception ex)
        {
            ToastService.Show("Export failed", ex.Message, "\uE783", settings: null);
        }
    }
}

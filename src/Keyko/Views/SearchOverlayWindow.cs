using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Keyko.Models;
using Keyko.Services;
using Keyko.ViewModels;

namespace Keyko.Views;

/// <summary>
/// The launcher overlay: fuzzy-search shortcuts, system actions and installed
/// apps from one glass panel. Enter runs the top hit, arrows navigate, Esc closes.
/// </summary>
public sealed class SearchOverlayWindow : Window
{
    private readonly MainViewModel _main;
    private readonly TextBox _search = new()
    {
        FontSize = 20,
        Watermark = "Type to search shortcuts, apps, actions…",
        BorderThickness = new Thickness(0),
        Background = Brushes.Transparent,
    };

    private readonly ListBox _results = new()
    {
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
    };

    private List<SearchResult> _items = new();
    private readonly List<string> _apps = new();

    private sealed record SearchResult(string Glyph, string Name, string Sub, Action Run, ShortcutItemViewModel? Item);

    public SearchOverlayWindow(MainWindow owner)
    {
        _main = (MainViewModel)owner.DataContext!;
        SystemDecorations = SystemDecorations.None;
        ShowActivated = false;
        ShowInTaskbar = false;
        Topmost = true;
        TransparencyLevelHint = [WindowTransparencyLevel.AcrylicBlur];
        Background = (IBrush?)Application.Current?.Resources["CardBgStrong"] ?? Brushes.Transparent;
        Width = 640;
        Height = 460;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Title = "Keyko Search";

        var stack = new StackPanel { Margin = new Thickness(16, 14) };
        stack.Children.Add(_search);
        stack.Children.Add(_results);
        Content = new Border
        {
            CornerRadius = new CornerRadius(16),
            Child = stack,
        };

        Position = CenterOn(owner);

        _search.TextChanged += (_, _) => Refill();
        _results.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && _results.SelectedItem is SearchResult r) { Run(r); e.Handled = true; }
        };
        _results.DoubleTapped += (_, _) => { if (_results.SelectedItem is SearchResult r) Run(r); };
        _search.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
            if (e.Key is Key.Up or Key.Down && _results.ItemCount > 0)
            {
                _results.Focus();
                var next = e.Key == Key.Down
                    ? Math.Min(_results.SelectedIndex + 1, _results.ItemCount - 1)
                    : Math.Max(_results.SelectedIndex - 1, 0);
                _results.SelectedIndex = _results.SelectedIndex < 0 ? 0 : next;
                e.Handled = true;
                _search.Focus();
            }
        };
        Opened += (_, _) => { _search.Focus(); Refill(); };

        ScanApps();
    }

    private PixelPoint CenterOn(Window owner)
    {
        var ox = owner.Position;
        var scale = owner.RenderScaling;
        var x = ox.X + (int)((owner.Bounds.Width - Width) * scale / 2);
        var y = ox.Y + (int)(120 * scale);
        return new PixelPoint(x, y);
    }

    private void Run(SearchResult r)
    {
        Close();
        r.Run();
    }

    private void Refill()
    {
        var q = _search.Text?.Trim() ?? "";
        var results = new List<SearchResult>();

        foreach (var item in _main.Items)
        {
            if (Fuzzy(q, item.Name) || Fuzzy(q, item.Category) || Fuzzy(q, item.HotkeyDisplay))
                results.Add(new SearchResult(item.TileFluentGlyph, item.Name,
                    item.HotkeyDisplay, () => _main.ExecuteAsync(item.Model, manual: true), item));
        }
        foreach (var a in SystemActionInfo.All)
        {
            if (Fuzzy(q, a.Name))
                results.Add(new SearchResult(a.Glyph, a.Name, "System action",
                    () => _main.ExecuteAsync(new ShortcutAction
                    {
                        Name = a.Name,
                        Type = ActionType.System,
                        Target = a.Kind.ToString(),
                        Category = "System",
                    }, manual: true), null));
        }
        foreach (var app in _apps)
        {
            if (Fuzzy(q, Path.GetFileNameWithoutExtension(app)))
                results.Add(new SearchResult("\uE71D", Path.GetFileNameWithoutExtension(app), "App",
                    () => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(app) { UseShellExecute = true }); } catch { } }, null));
        }

        if (q.Length == 0)
        {
            // top 8 by usage, then the rest alphabetically
            results = results.OrderByDescending(r => r.Item?.Model.RunCount ?? 0).Take(12).ToList();
        }

        _items = results.Take(12).ToList();
        _results.ItemsSource = _items;
        if (_results.ItemCount > 0) _results.SelectedIndex = 0;
    }

    private static bool Fuzzy(string query, string text)
    {
        if (query.Length == 0) return true;
        if (text is null) return false;
        int ti = 0;
        foreach (var c in query.ToLowerInvariant())
        {
            var idx = text.ToLowerInvariant().IndexOf(c, ti);
            if (idx < 0) return false;
            ti = idx + 1;
        }
        return true;
    }

    private void ScanApps()
    {
        try
        {
            var dirs = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            };
            foreach (var dir in dirs.Where(d => d.Length > 0))
                foreach (var f in Directory.EnumerateFiles(dir, "*.lnk", SearchOption.AllDirectories))
                    _apps.Add(f);
            _apps.Sort();
        }
        catch { /* start menu scan is best-effort */ }
    }
}

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Keyko.Models;

namespace Keyko.Services;

/// <summary>
/// Non-focus-stealing toasts near the bottom-right of the primary screen.
/// Solid near-opaque card (no acrylic â raw blur read as a smudge at this size),
/// clean Fluent glyph instead of an icon tile.
/// </summary>
public sealed class ToastService
{
    private static readonly List<ToastWindow> Open = new();

    public static void Show(string title, string? subtitle = null, string glyph = "\uE73E",
        AppSettings? settings = null, (string Label, Action Click)? action = null)
    {
        if (settings is { ShowToasts: false } && action is null) return;

        // InvokeAsync (Normal priority) â Post runs at Background priority, which the
        // modal dialog loop starves, leaving toasts invisible while a dialog is open
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            try
            {
                var w = new ToastWindow(title, subtitle, glyph, action);
                w.Closed += (_, _) => Open.Remove(w);
                Open.Add(w);
                w.Show();
            }
            catch { /* toasts must never crash the app */ }
        });
    }

    // synchronous variant for diagnostics and the selftest (UI thread)
    public static void ShowNow(string title, string? subtitle = null, string glyph = "",
        AppSettings? settings = null, (string Label, Action Click)? action = null)
    {
        if (settings is { ShowToasts: false } && action is null) return;
        try
        {
            var w = new ToastWindow(title, subtitle, glyph, action);
            w.Closed += (_, _) => Open.Remove(w);
            Open.Add(w);
            w.Show();
        }
        catch { /* toasts must never crash the app */ }
    }

    public static void MoveStackUp()
    {
        // relayout remaining toasts after one closes
        for (int i = 0; i < Open.Count; i++)
            Open[i].SlideTo(i);
    }

    private sealed class ToastWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private readonly int _stackIndex;

        public ToastWindow(string title, string? subtitle, string glyph, (string Label, Action Click)? action)
        {
            _stackIndex = Open.Count;
            SystemDecorations = SystemDecorations.None;
            ShowActivated = false;
            ShowInTaskbar = false;
            Topmost = true;
            Background = Brushes.Transparent;
            Width = 384;
            Height = 88;
            Opacity = 0;

            var icon = new TextBlock
            {
                Text = glyph,
                FontSize = 19,
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                Foreground = (IBrush?)Application.Current?.Resources["Accent1"],
                VerticalAlignment = VerticalAlignment.Center,
            };

            var textCol = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
            textCol.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 15,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            });
            if (!string.IsNullOrEmpty(subtitle))
                textCol.Children.Add(new TextBlock
                {
                    Text = subtitle,
                    FontFamily = (FontFamily?)Application.Current?.Resources["SerifItalic"],
                    FontSize = 12.5,
                    Opacity = 0.72,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                });

            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, Margin = new Thickness(18, 0) };
            row.Children.Add(icon);
            row.Children.Add(textCol);

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            Grid.SetColumn(row, 0);
            grid.Children.Add(row);

            if (action is { } act)
            {
                var btn = new Button
                {
                    Content = act.Label,
                    Classes = { "ghost" },
                    VerticalAlignment = VerticalAlignment.Center,
                };
                btn.Click += (_, _) => { try { act.Click(); } catch { } CloseNow(); };
                Grid.SetColumn(btn, 1);
                grid.Children.Add(btn);
            }

            var card = new Border
            {
                CornerRadius = new CornerRadius(16),
                Background = (IBrush?)Application.Current?.Resources["CardBgStrong"],
                BorderBrush = (IBrush?)Application.Current?.Resources["CardBorder"],
                BorderThickness = new Thickness(1),
                BoxShadow = BoxShadows.Parse("0 14 40 -12 #88000000"),
                Child = grid,
            };

            Content = new Grid { VerticalAlignment = VerticalAlignment.Center, Children = { card } };

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(action is null ? 4200 : 7000) };
            _timer.Tick += async (_, _) =>
            {
                _timer.Stop();
                try { await FadeTo(0, 260); } catch { }
                CloseNow();
            };
        }

        private Border WrapCard(Control content) => new()
        {
            CornerRadius = new CornerRadius(16),
            Background = (IBrush?)Application.Current?.Resources["CardBgStrong"],
            BorderBrush = (IBrush?)Application.Current?.Resources["CardBorder"],
            BorderThickness = new Thickness(1),
            BoxShadow = BoxShadows.Parse("0 14 40 -12 #88000000"),
            Child = content,
        };

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            SlideTo(_stackIndex);
            _ = FadeTo(1, 160);
            _timer.Start();
        }

        public void SlideTo(int index)
        {
            try
            {
                var screen = Screens?.Primary;
                if (screen is null) return;
                var wa = screen.WorkingArea;
                var scale = RenderScaling;
                var h = (Bounds.Height * scale) + 12;
                var x = (int)(wa.X + wa.Width - (Bounds.Width * scale) - 18);
                var y = (int)(wa.Y + wa.Height - h * (index + 1) - 18);
                Position = new PixelPoint(x, y);
            }
            catch { }
        }

        private void CloseNow()
        {
            _timer.Stop();
            try { Close(); } catch { }
            ToastService.MoveStackUp();
        }

        private async System.Threading.Tasks.Task FadeTo(double opacity, int ms)
        {
            var anim = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(ms),
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0.0),
                        Setters = { new Setter(OpacityProperty, Opacity) },
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1.0),
                        Setters = { new Setter(OpacityProperty, opacity) },
                    },
                },
            };
            await anim.RunAsync(this);
        }
    }
}

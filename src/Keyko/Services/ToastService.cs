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
/// Small non-focus-stealing glass toasts near the bottom-right of the primary screen.
/// </summary>
public sealed class ToastService
{
    private static readonly List<ToastWindow> Open = new();

    public static void Show(string title, string? subtitle = null, string emoji = "🌸",
        AppSettings? settings = null, (string Label, Action Click)? action = null)
    {
        if (settings is { ShowToasts: false } && action is null) return;

        // InvokeAsync (Normal priority) — Post runs at Background priority, which the
        // modal dialog loop starves, leaving toasts invisible while a dialog is open
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            try
            {
                var w = new ToastWindow(title, subtitle, emoji, action);
                w.Closed += (_, _) => Open.Remove(w);
                Open.Add(w);
                w.Show();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("toast failed: " + ex.Message);
            }
        });
    }
    // synchronous variant for diagnostics/selftest (runs on the UI thread)
    public static void ShowNow(string title, string? subtitle = null, string emoji = "🌸",
        AppSettings? settings = null, (string Label, Action Click)? action = null)
    {
        if (settings is { ShowToasts: false } && action is null) return;
        try
        {
            var w = new ToastWindow(title, subtitle, emoji, action);
            w.Closed += (_, _) => Open.Remove(w);
            Open.Add(w);
            w.Show();
        }
        catch (Exception ex) { Console.Error.WriteLine("toast failed: " + ex.Message); }
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

        public ToastWindow(string title, string? subtitle, string emoji, (string Label, Action Click)? action)
        {
            _stackIndex = Open.Count;
            SystemDecorations = SystemDecorations.None;
            ShowActivated = false;
            ShowInTaskbar = false;
            Topmost = true;
            Background = Brushes.Transparent;
            Width = 384;
            Height = 92;
            SizeToContent = SizeToContent.Manual;
            Opacity = 0;

            var tile = new Border
            {
                Width = 42,
                Height = 42,
                CornerRadius = new CornerRadius(13),
                Background = (IBrush?)Application.Current?.Resources["AccentGradient"],
                Child = new TextBlock
                {
                    Text = emoji,
                    FontSize = 20,
                    FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
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
                    MaxHeight = 34,
                });

            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 13, Margin = new Thickness(16, 14) };
            row.Children.Add(tile);
            row.Children.Add(textCol);

            Border card;
            if (action is { } act)
            {
                var btn = new Button
                {
                    Content = act.Label,
                    Classes = { "ghost" },
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeight.Medium,
                };
                btn.Click += (_, _) => { try { act.Click(); } catch { } CloseNow(); };
                var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                Grid.SetColumn(row, 0);
                Grid.SetColumn(btn, 1);
                grid.Children.Add(row);
                grid.Children.Add(btn);
                row.Margin = new Thickness(14, 12, 4, 12);
                card = WrapCard(grid);
            }
            else
            {
                card = WrapCard(row);
            }

            Content = card;

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
            CornerRadius = new CornerRadius(18),
            Background = (IBrush?)Application.Current?.Resources["CardBgStrong"],
            BorderBrush = (IBrush?)Application.Current?.Resources["CardBorder"],
            BorderThickness = new Thickness(1),
            BoxShadow = BoxShadows.Parse("0 12 36 -10 #66000000"),
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
                var h = (Bounds.Height * scale) + 10;
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

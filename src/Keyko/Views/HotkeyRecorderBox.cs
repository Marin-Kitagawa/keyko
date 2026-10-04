using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Keyko.Models;

namespace Keyko.Views;

/// <summary>
/// Small reusable recorder: a bordered box that captures the next key combo.
/// Reports via the HotkeyChanged event; Esc clears.
/// </summary>
public sealed class HotkeyRecorderBox : Border
{
    private readonly TextBlock _label = new()
    {
        FontSize = 14,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };

    public static readonly StyledProperty<string> WatermarkProperty =
        AvaloniaProperty.Register<HotkeyRecorderBox, string>(nameof(Watermark), "Click, then press keys…");

    public string Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    public event Action<HotkeyGesture>? HotkeyChanged;
    public event Action? Cleared;

    public void Unwire() => HotkeyChanged = null;
    public HotkeyRecorderBox()
    {
        Height = 44;
        CornerRadius = new CornerRadius(10);
        Background = (IBrush?)Application.Current?.Resources["InputBg"];
        BorderBrush = (IBrush?)Application.Current?.Resources["CardBorder"];
        BorderThickness = new Thickness(1);
        Child = _label;
        _label.Text = Watermark;
        Focusable = true;

        PointerPressed += (_, _) => Focus();
        LostFocus += (_, _) => _label.Opacity = 0.8;
        GotFocus += (_, _) => _label.Opacity = 1;
        KeyDown += (_, e) =>
        {
            var key = e.Key;
            e.Handled = true;
            if (key is Key.None or Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
                or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
                return;
            if (key == Key.Escape) { Cleared?.Invoke(); return; }
            HotkeyChanged?.Invoke(new HotkeyGesture(key, e.KeyModifiers));
        };
    }

    public void SetGesture(HotkeyGesture? g) => _label.Text = g?.Display ?? Watermark;
}

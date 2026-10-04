using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Layout;
using Avalonia.Threading;

namespace Keyko.Services;

/// <summary>
/// Screen-tool overlays: the color picker (click copies hex) and the OCR
/// region selector (drag a rectangle, text goes to the clipboard).
/// Both are fullscreen borderless topmost windows that never steal focus.
/// </summary>
public static class Overlays
{
    private static ColorPickerWindow? _colorPicker;
    private static OcrWindow? _ocr;

    public static void ShowColorPicker()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_colorPicker is null)
            {
                _colorPicker = new ColorPickerWindow();
                _colorPicker.Closed += (_, _) => _colorPicker = null;
            }
            _colorPicker.Show();
        });
    }

    public static void ShowOcrRegion()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_ocr is null)
            {
                _ocr = new OcrWindow();
                _ocr.Closed += (_, _) => _ocr = null;
            }
            _ocr.Show();
        });
    }
}

/// <summary>Fullscreen overlay: hover shows a live color chip, click copies hex.</summary>
public sealed class ColorPickerWindow : Window
{
    private readonly Border _chip;
    private readonly TextBlock _hexText = new()
    {
        FontSize = 12,
        Foreground = Brushes.White,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        Text = "#——",
    };

    public ColorPickerWindow()
    {
        SystemDecorations = SystemDecorations.None;
        ShowActivated = false;
        Topmost = true;
        Background = new SolidColorBrush(new Color(1, 0, 0, 0));
        WindowState = WindowState.Maximized;
        Cursor = Cursor.Parse("Cross");

        _chip = new Border
        {
            Width = 84, Height = 46,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromRgb(0x22, 0x18, 0x2B)),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1),
            Child = _hexText,
        };

        var canvas = new Canvas();
        canvas.Children.Add(_chip);
        Content = canvas;

        PointerMoved += (_, e) =>
        {
            var p = e.GetCurrentPoint(this).Position;
            var hex = ScreenPixel(p);
            _hexText.Text = hex;
            _chip.Background = new SolidColorBrush(Color.Parse(hex));
            Canvas.SetLeft(_chip, p.X - 105);
            Canvas.SetTop(_chip, p.Y > 90 ? p.Y + 20 : p.Y - 70);
        };

        PointerPressed += (_, e) =>
        {
            var p = e.GetCurrentPoint(this).Position;
            var hex = ScreenPixel(p);
            ClipboardText.SetText(hex);
            ToastService.Show("Color copied", hex, "\uE790", settings: AppServices.Config.Settings);
            Close();
            e.Handled = true;
        };
    }

    private string ScreenPixel(Point p)
    {
        var px = ((Window)this).WindowToScreen(p);
        var dc = GetDC(IntPtr.Zero);
        try
        {
            var c = GetPixel(dc, px.X, px.Y);
            return $"#{(c & 0xFF):X2}{((c >> 8) & 0xFF):X2}{((c >> 16) & 0xFF):X2}";
        }
        finally { ReleaseDC(IntPtr.Zero, dc); }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern uint GetPixel(IntPtr dc, int x, int y);
}

/// <summary>Fullscreen overlay: drag a rectangle, the region is OCR'd into the clipboard.</summary>
public sealed class OcrWindow : Window
{
    private readonly Border _selection = new()
    {
        BorderBrush = new SolidColorBrush(Color.FromRgb(0xF4, 0x72, 0xB6)),
        BorderThickness = new Thickness(2),
        CornerRadius = new CornerRadius(2),
        IsHitTestVisible = false,
        IsVisible = false,
    };

    private readonly TextBlock _hint = new()
    {
        Text = "Drag a rectangle over the text to copy — Esc cancels",
        Foreground = Brushes.White,
        FontSize = 13,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        Margin = new Thickness(0, 24, 0, 0),
    };

    private Point? _start;

    public OcrWindow()
    {
        SystemDecorations = SystemDecorations.None;
        ShowActivated = false;
        Topmost = true;
        Background = new SolidColorBrush(new Color(60, 0, 0, 0));
        WindowState = WindowState.Maximized;
        Content = new Panel { Children = { _selection, _hint } };
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Focus();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        _start = e.GetCurrentPoint(this).Position;
        Canvas.SetLeft(_selection, _start.Value.X);
        Canvas.SetTop(_selection, _start.Value.Y);
        _selection.Width = 0;
        _selection.Height = 0;
        _selection.IsVisible = true;
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (_start is not { } s) return;
        var p = e.GetCurrentPoint(this).Position;
        Canvas.SetLeft(_selection, Math.Min(s.X, p.X));
        Canvas.SetTop(_selection, Math.Min(s.Y, p.Y));
        _selection.Width = Math.Abs(p.X - s.X);
        _selection.Height = Math.Abs(p.Y - s.Y);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_start is not { } s) { e.Handled = true; return; }
        var p = e.GetCurrentPoint(this).Position;
        var scale = (int)RenderScaling;
        var rect = new PixelRect(
            (int)Math.Min(s.X, p.X) * scale,
            (int)Math.Min(s.Y, p.Y) * scale,
            Math.Max(4, (int)Math.Abs(p.X - s.X) * scale),
            Math.Max(4, (int)Math.Abs(p.Y - s.Y) * scale));
        _start = null;
        _selection.IsVisible = false;
        Hide();
        _ = RunOcrAsync(rect);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
        e.Handled = true;
    }

    private async Task RunOcrAsync(PixelRect rect)
    {
        try
        {
            using var bmp = new System.Drawing.Bitmap(rect.Width, rect.Height);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
                g.CopyFromScreen(rect.X, rect.Y, 0, 0, new System.Drawing.Size(rect.Width, rect.Height));

            var text = await OcrHelper.RecognizeAsync(bmp);
            if (string.IsNullOrWhiteSpace(text))
            {
                ToastService.Show("No text found", "Try a larger area or more contrast.", "\uE81C", settings: null);
            }
            else
            {
                await ClipboardText.SetTextAsync(text.Trim());
                ToastService.Show("Text copied", $"{text.Trim().Split('\n').Length} lines recognized", "\uE8E9", settings: null);
            }
        }
        catch (Exception ex)
        {
            ToastService.Show("OCR failed", ex.Message, "\uE783", settings: null);
        }
        Close();
    }
}

public static class OcrHelper
{
    /// <summary>Recognizes text in a bitmap using the Windows OCR engine (user's language).</summary>
    public static async Task<string> RecognizeAsync(System.Drawing.Bitmap bmp)
    {
        var engine = global::Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages()
                     ?? global::Windows.Media.Ocr.OcrEngine.TryCreateFromLanguage(new global::Windows.Globalization.Language("en-US"));
        if (engine is null) throw new InvalidOperationException("No OCR engine available for this language.");

        var bd = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
            System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[bd.Stride * bmp.Height];
            Marshal.Copy(bd.Scan0, bytes, 0, bytes.Length);
            var buffer = System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.AsBuffer(bytes);
            var software = global::Windows.Graphics.Imaging.SoftwareBitmap.CreateCopyFromBuffer(
                buffer,
                global::Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                bmp.Width, bmp.Height,
                global::Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);
            var result = await engine.RecognizeAsync(software);
            return result.Text;
        }
        finally { bmp.UnlockBits(bd); }
    }
}

public static class OverlayPointExtensions
{
    /// <summary>Fullscreen maximized overlay: local point -> absolute screen pixels.</summary>
    public static PixelPoint WindowToScreen(this Window w, Point p)
    {
        var pos = w.Position;
        var scale = w.RenderScaling;
        return new PixelPoint(pos.X + (int)(p.X * scale), pos.Y + (int)(p.Y * scale));
    }
}

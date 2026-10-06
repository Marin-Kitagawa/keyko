using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Keyko.Models;

namespace Keyko.Services;

/// <summary>
/// Mutates the shared brushes defined in App.axaml so the whole UI re-skins live.
/// StaticResource keeps referencing the same brush instances; we just change their colors.
/// </summary>
public static class UiTheme
{
    public static bool IsDark { get; private set; } = true;

    private static readonly Dictionary<string, SolidColorBrush> _solid = new();
    private static LinearGradientBrush? _accent;
    private static LinearGradientBrush? _accentHover;

    public static readonly (string Name, string C1, string C2)[] Accents =
    {
        ("Blossom",  "#F472B6", "#A78BFA"),
        ("Sakura",   "#F8BBD0", "#F5C5DA"),
        ("Frost",    "#A5D8FF", "#C5F6FA"),
        ("Sunset",   "#FF9A8B", "#FF6A88"),
        ("Cappuccino", "#D5B895", "#A67B5B"),
        ("Rose",     "#FB7185", "#FDA4AF"),
        ("Peach",    "#FDBA74", "#F9A8D4"),
        ("Lavender", "#A78BFA", "#93C5FD"),
        ("Mint",     "#6EE7B7", "#99F6E4"),
        ("Sky",      "#93C5FD", "#C4B5FD"),
    };

    public static void Init(Application app)
    {
        _solid.Clear();
        foreach (var (k, v) in app.Resources)
            if (v is SolidColorBrush b && k is string ks) _solid[ks] = b;

        _accent = app.Resources["AccentGradient"] as LinearGradientBrush;
        _accentHover = app.Resources["AccentGradientHover"] as LinearGradientBrush;

        Apply(
            app: app,
            theme: "Dark",
            accent1: "#F472B6",
            accent2: "#A78BFA");
    }

    private static Color Parse(string hex) => (Color)Color.Parse(hex);

    public static void Apply(Application app, string theme, string accent1, string accent2)
    {
        IsDark = !string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase);
        var c1 = Parse(accent1);
        var c2 = Parse(accent2);

        Set("Accent1", c1);
        Set("Accent2", c2);
        if (_accent is { } g)
        {
            g.GradientStops[0].Color = c1;
            g.GradientStops[1].Color = c2;
        }
        if (_accentHover is { } gh)
        {
            gh.GradientStops[0].Color = Lighten(c1, 0.10);
            gh.GradientStops[1].Color = Lighten(c2, 0.10);
        }

        // Fluent accent (toggles, checks, selection, focus rings)
        app.Resources["SystemAccentColor"] = c1;
        app.Resources["SystemAccentColorDark1"] = Darken(c1, 0.15);
        app.Resources["SystemAccentColorDark2"] = Darken(c1, 0.30);
        app.Resources["SystemAccentColorDark3"] = Darken(c1, 0.45);
        app.Resources["SystemAccentColorLight1"] = Lighten(c1, 0.15);
        app.Resources["SystemAccentColorLight2"] = Lighten(c1, 0.30);
        app.Resources["SystemAccentColorLight3"] = Lighten(c1, 0.45);

        // Fluent variant so native control glyphs match
        app.RequestedThemeVariant = IsDark
            ? Avalonia.Styling.ThemeVariant.Dark
            : Avalonia.Styling.ThemeVariant.Light;

        var acc1 = c1;
        if (IsDark)
        {
            Set("TextPrimary",       C(0xFB, 0xF3, 0xF8));
            Set("TextSecondary",     C(0xCB, 0xBC, 0xD2));
            Set("TextTertiary",      C(0x9E, 0x92, 0xAA));
            Set("CardBg",            C(0xFF, 0xFF, 0xFF, 0x0A));
            Set("CardBgHover",       C(0xFF, 0xFF, 0xFF, 0x14));
            Set("CardBgStrong",      C(0x22, 0x18, 0x2B, 0xF2));
            Set("InputBg",           C(0xFF, 0xFF, 0xFF, 0x07));
            Set("InputBgHover",      C(0xFF, 0xFF, 0xFF, 0x0D));
            Set("CardBorder",        C(0xFF, 0xFF, 0xFF, 0x16));
            Set("CardBorderHover",   C(acc1.R, acc1.G, acc1.B, 0x66));
            Set("Divider",           C(0xFF, 0xFF, 0xFF, 0x12));
            Set("ChipBg",            C(0xFF, 0xFF, 0xFF, 0x0D));
            Set("ChipBgHover",       C(0xFF, 0xFF, 0xFF, 0x1A));
            Set("KbdBg",             C(0xFF, 0xFF, 0xFF, 0x12));
            Set("KbdBorder",         C(0xFF, 0xFF, 0xFF, 0x24));
            Set("KbdText",           C(0xD9, 0xDD, 0xEA));
            Set("NavActiveBg",       C(acc1.R, acc1.G, acc1.B, 0x2E));
            Set("BtnGhostHover",     C(0xFF, 0xFF, 0xFF, 0x0F));
            Set("BtnHover",          C(0xFF, 0xFF, 0xFF, 0x14));
            Set("ScrollThumb",       C(0xFF, 0xFF, 0xFF, 0x28));
            Set("ScrollThumbHover",  C(0xFF, 0xFF, 0xFF, 0x42));
            Set("Danger",            C(0xFB, 0x71, 0x85));
            Set("Warning",           C(0xFC, 0xC4, 0x6D));
            Set("Success",           C(0x6E, 0xE7, 0xB7));
            Set("DangerBg",          C(0xFB, 0x71, 0x85, 0x24));
            Set("WarningBg",         C(0xFC, 0xC4, 0x6D, 0x1C));
            Set("BannerBorder",      C(0xFC, 0xC4, 0x6D, 0x59));
            Set("Base1",             C(0x17, 0x12, 0x1D));
            Set("Base2",             C(0x1F, 0x15, 0x28));
            Set("Base3",             C(0x2A, 0x18, 0x30));
            Set("TileDim",           C(0x00, 0x00, 0x00, 0x66));
        }
        else
        {
            Set("TextPrimary",       C(0x30, 0x22, 0x33));
            Set("TextSecondary",     C(0x62, 0x50, 0x66));
            Set("TextTertiary",      C(0x8E, 0x7C, 0x96));
            Set("CardBg",            C(0xFF, 0xFF, 0xFF, 0x8C));
            Set("CardBgHover",       C(0xFF, 0xFF, 0xFF, 0xD0));
            Set("CardBgStrong",      C(0xFF, 0xFF, 0xFF, 0xFA));
            Set("InputBg",           C(0xFF, 0xFF, 0xFF, 0x99));
            Set("InputBgHover",      C(0xFF, 0xFF, 0xFF, 0xC2));
            Set("CardBorder",        C(0x50, 0x30, 0x58, 0x22));
            Set("CardBorderHover",   C(acc1.R, acc1.G, acc1.B, 0x78));
            Set("Divider",           C(0x50, 0x30, 0x58, 0x1C));
            Set("ChipBg",            C(0x50, 0x30, 0x58, 0x0F));
            Set("ChipBgHover",       C(0x50, 0x30, 0x58, 0x1A));
            Set("KbdBg",             C(0xFF, 0xFF, 0xFF, 0xB4));
            Set("KbdBorder",         C(0x14, 0x18, 0x28, 0x2E));
            Set("KbdText",           C(0x44, 0x33, 0x49));
            Set("NavActiveBg",       C(acc1.R, acc1.G, acc1.B, 0x2B));
            Set("BtnGhostHover",     C(0x50, 0x30, 0x58, 0x0D));
            Set("BtnHover",          C(0xFF, 0xFF, 0xFF, 0xE0));
            Set("ScrollThumb",       C(0x50, 0x30, 0x58, 0x3C));
            Set("ScrollThumbHover",  C(0x50, 0x30, 0x58, 0x5C));
            Set("Danger",            C(0xE1, 0x4B, 0x6C));
            Set("Warning",           C(0xB4, 0x53, 0x09));
            Set("Success",           C(0x05, 0x96, 0x69));
            Set("DangerBg",          C(0xE1, 0x4B, 0x6C, 0x14));
            Set("WarningBg",         C(0xB4, 0x53, 0x09, 0x12));
            Set("BannerBorder",      C(0xB4, 0x53, 0x09, 0x44));
            Set("Base1",             C(0xFB, 0xEF, 0xF4));
            Set("Base2",             C(0xFC, 0xF1, 0xF6));
            Set("Base3",             C(0xF4, 0xEB, 0xFB));
            Set("TileDim",           C(0xFF, 0xFF, 0xFF, 0x40));
        }

        GlassService.NotifyChanged();
    }

    private static void Set(string key, Color c)
    {
        if (_solid.TryGetValue(key, out var b)) b.Color = c;
    }

    public static Color C(byte r, byte g, byte b, byte a = 0xFF) => Color.FromArgb(a, r, g, b);

    public static Color Darken(Color c, double f) => Color.FromArgb(
        c.A,
        (byte)(c.R * (1 - f)),
        (byte)(c.G * (1 - f)),
        (byte)(c.B * (1 - f)));

    public static Color Lighten(Color c, double f) => Color.FromArgb(
        c.A,
        (byte)(c.R + (255 - c.R) * f),
        (byte)(c.G + (255 - c.G) * f),
        (byte)(c.B + (255 - c.B) * f));

    public static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    public static Color ParseOr(string hex, Color fallback)
    {
        try { return Parse(hex); } catch { return fallback; }
    }

    public static IBrush Brush(string key) =>
        _solid.TryGetValue(key, out var b) ? b : Brushes.Transparent;

    private static readonly Color[] CategoryColors =
    {
        Parse("#F472B6"), Parse("#A78BFA"), Parse("#FDBA74"), Parse("#FB7185"),
        Parse("#6EE7B7"), Parse("#93C5FD"), Parse("#C4B5FD"), Parse("#F9A8D4"),
    };

    public static Color CategoryColor(string category)
    {
        var h = Math.Abs(string.IsNullOrWhiteSpace(category) ? 0 : category.GetHashCode()) % CategoryColors.Length;
        return CategoryColors[h];
    }
}

/// <summary>
/// Pushes glass settings into every open window's acrylic material.
/// </summary>
public static class GlassService
{
    public static event Action? Changed;

    public static void NotifyChanged() => Changed?.Invoke();

    public static void ApplyTo(ExperimentalAcrylicMaterial m, AppSettings settings)
    {
        m.BackgroundSource = AcrylicBackgroundSource.Digger;
        m.TintColor = UiTheme.IsDark ? Color.Parse("#17121D") : Color.Parse("#FBF0F5");
        m.TintOpacity = Clamp(settings.GlassTintOpacity, 0.30, 0.95);
        m.MaterialOpacity = UiTheme.IsDark ? 0.55 : 0.62;
    }

    public static double Clamp(double v, double lo, double hi) => Math.Clamp(v, lo, hi);
}

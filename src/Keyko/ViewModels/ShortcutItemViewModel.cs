using System;
using System.IO;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Keyko.Models;
using Keyko.Services;

namespace Keyko.ViewModels;

public partial class ShortcutItemViewModel : ObservableObject
{
    public ShortcutAction Model { get; }

    private readonly string _iconsDir;

    public event Action<ShortcutItemViewModel>? Toggled;

    public ShortcutItemViewModel(ShortcutAction model, string iconsDir)
    {
        Model = model;
        _iconsDir = iconsDir;
        _isEnabled = model.Enabled;
        RebuildVisuals(reloadIcon: true);
    }

    public string Id => Model.Id;

    public string Name => string.IsNullOrWhiteSpace(Model.Name) ? "(unnamed)" : Model.Name;

    public string Description => string.IsNullOrWhiteSpace(Model.Description) ? Model.TargetSummary : Model.Description!;

    public string TargetSummary => Model.TargetSummary;

    public string HotkeyDisplay => Model.Gesture?.Display ?? "No hotkey";

    public bool HasHotkey => Model.Gesture is not null;

    public string Category => Model.Category;

    public string TileGlyph => Model.TileGlyph;

    /// <summary>Segoe Fluent glyph shown when the user hasn't picked an emoji.</summary>
    public string TileFluentGlyph => Model.Type switch
    {
        ActionType.Application => "\uE71D",
        ActionType.Folder => "\uE8B7",
        ActionType.Url => "\uE774",
        ActionType.Command => "\uE756",
        ActionType.Snippet => "\uE77F",
        ActionType.KeySequence => "\uE765",
        ActionType.System => "\uE945",
        _ => "\uE765",
    };

    public bool TileIsEmoji => !string.IsNullOrEmpty(Model.Emoji);

    public bool TileUseFluentGlyph => !TileIsEmoji && !HasIcon;

    public IBrush CategoryBrush { get; private set; } = Brushes.Transparent;

    public IBrush CategoryFgBrush { get; private set; } = Brushes.Transparent;

    public IBrush TileBrush { get; private set; } = Brushes.Transparent;

    public IImage? IconImage { get; private set; }

    public bool HasIcon => IconImage is not null;

    public string RunCountText => Model.RunCount switch
    {
        0 => "Never run",
        1 => "1 launch",
        _ => $"{Model.RunCount} launches",
    };

    public string LastUsedText => Model.LastUsedAt is { } t
        ? $"last {FormatAge(DateTime.Now - t)}"
        : "";

    [ObservableProperty]
    private bool _isEnabled;

    partial void OnIsEnabledChanged(bool value)
    {
        if (Model.Enabled != value)
        {
            Model.Enabled = value;
            Toggled?.Invoke(this);
        }
    }

    public void Refresh(bool reloadIcon = false)
    {
        IsEnabled = Model.Enabled;
        RebuildVisuals(reloadIcon);
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(TargetSummary));
        OnPropertyChanged(nameof(HotkeyDisplay));
        OnPropertyChanged(nameof(HasHotkey));
        OnPropertyChanged(nameof(Category));
        OnPropertyChanged(nameof(TileGlyph));
        OnPropertyChanged(nameof(RunCountText));
        OnPropertyChanged(nameof(LastUsedText));
        OnPropertyChanged(nameof(IconImage));
        OnPropertyChanged(nameof(HasIcon));
        OnPropertyChanged(nameof(CategoryBrush));
        OnPropertyChanged(nameof(CategoryFgBrush));
        OnPropertyChanged(nameof(TileBrush));
    }

    private void RebuildVisuals(bool reloadIcon = true)
    {
        var c = UiTheme.CategoryColor(Model.Category);
        CategoryBrush = new SolidColorBrush(UiTheme.WithAlpha(c, 0x2E));
        CategoryFgBrush = new SolidColorBrush(UiTheme.IsDark ? c : UiTheme.Darken(c, 0.25));
        TileBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(c, 0),
                new GradientStop(UiTheme.Darken(c, 0.38), 1),
            },
        };

        if (reloadIcon && Model.Type == ActionType.Application)
        {
            var path = IconCacheService.GetIconPath(Model, _iconsDir);
            IconImage = path is not null && File.Exists(path) ? LoadBitmap(path) : null;
        }
        else if (Model.Type != ActionType.Application)
        {
            IconImage = null;
        }
    }

    private static IImage? LoadBitmap(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            return new Bitmap(fs);
        }
        catch
        {
            return null;
        }
    }

    private static string FormatAge(TimeSpan age) => age.TotalMinutes switch
    {
        < 1 => "just now",
        < 60 => $"{(int)age.TotalMinutes} min ago",
        < 60 * 24 => $"{(int)age.TotalHours} h ago",
        < 60 * 24 * 7 => $"{(int)age.TotalDays} d ago",
        _ => $"{(int)(age.TotalDays / 7)} w ago",
    };
}

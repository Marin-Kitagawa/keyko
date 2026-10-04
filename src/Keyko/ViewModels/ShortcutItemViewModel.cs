using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Keyko.Models;
using Keyko.Services;

namespace Keyko.ViewModels;

public partial class ShortcutItemViewModel : ObservableObject
{
    public ShortcutAction Model { get; }

    public event Action<ShortcutItemViewModel>? Toggled;

    public ShortcutItemViewModel(ShortcutAction model)
    {
        Model = model;
        _isEnabled = model.Enabled;
        RebuildVisuals();
    }

    public string Id => Model.Id;

    public string Name => string.IsNullOrWhiteSpace(Model.Name) ? "(unnamed)" : Model.Name;

    public string Description => string.IsNullOrWhiteSpace(Model.Description) ? Model.TargetSummary : Model.Description!;

    public string TargetSummary => Model.TargetSummary;

    public string HotkeyDisplay => Model.Gesture?.Display ?? "No hotkey";

    public bool HasHotkey => Model.Gesture is not null;

    public string Category => Model.Category;

    /// <summary>Segoe Fluent glyph for the action type — the single consistent icon voice.</summary>
    public string TileFluentGlyph => Model.Type switch
    {
        ActionType.Application => "\uE71D",
        ActionType.Folder => "\uE8B7",
        ActionType.Url => "\uE774",
        ActionType.Command => "\uE756",
        ActionType.Snippet => "\uE77F",
        ActionType.KeySequence => "\uE765",
        ActionType.System => "\uE945",
        ActionType.Webhook => "\uE8EA",
        ActionType.Script => "\uE943",
        ActionType.Expansion => "\uE77B",
        _ => "\uE765",
    };

    public IBrush CategoryBrush { get; private set; } = Brushes.Transparent;

    public IBrush CategoryFgBrush { get; private set; } = Brushes.Transparent;

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

    public void Refresh()
    {
        IsEnabled = Model.Enabled;
        RebuildVisuals();
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(TargetSummary));
        OnPropertyChanged(nameof(HotkeyDisplay));
        OnPropertyChanged(nameof(HasHotkey));
        OnPropertyChanged(nameof(Category));
        OnPropertyChanged(nameof(RunCountText));
        OnPropertyChanged(nameof(LastUsedText));
        OnPropertyChanged(nameof(CategoryBrush));
        OnPropertyChanged(nameof(CategoryFgBrush));
    }

    private void RebuildVisuals()
    {
        var c = UiTheme.CategoryColor(Model.Category);
        CategoryBrush = new SolidColorBrush(UiTheme.WithAlpha(c, 0x2E));
        CategoryFgBrush = new SolidColorBrush(UiTheme.IsDark ? c : UiTheme.Darken(c, 0.25));
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

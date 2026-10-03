using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Keyko.Models;

namespace Keyko.ViewModels;

public partial class ShortcutEditorViewModel : ViewModelBase
{
    private readonly IEnumerable<ShortcutAction> _existing;
    private readonly Func<System.Action>? _testRun;

    public ShortcutEditorViewModel(ShortcutAction model, IEnumerable<ShortcutAction> existing,
        Func<System.Action>? testRun = null)
    {
        Model = model;
        _existing = existing;
        _testRun = testRun;

        _name = model.Name;
        _description = model.Description ?? "";
        _category = model.Category;
        _targetText = model.Type == ActionType.System || model.Type == ActionType.KeySequence ? "" : model.Target;
        _arguments = model.Arguments ?? "";
        _workingDirectory = model.WorkingDirectory ?? "";
        _snippetText = model.Type == ActionType.Snippet ? model.Target : "";
        _sequenceText = model.Type == ActionType.KeySequence ? model.Target : "";
        _systemActionIndex = model.Type == ActionType.System
            ? Math.Max(0, Array.FindIndex(SystemActionInfo.All, x => x.Kind.ToString() == model.Target))
            : 0;
        _hotkeyDisplay = model.Gesture?.Display ?? "";
        _emoji = model.Emoji ?? "";
        _enabled = model.Enabled;
        _showToast = model.ShowToast;

        SetTypeFlags(model.Type);
    }

    public ShortcutAction Model { get; }

    public bool IsNew { get; init; }

    // ---- type selector ----
    [ObservableProperty] private bool _isTypeApplication;
    [ObservableProperty] private bool _isTypeFolder;
    [ObservableProperty] private bool _isTypeUrl;
    [ObservableProperty] private bool _isTypeCommand;
    [ObservableProperty] private bool _isTypeSnippet;
    [ObservableProperty] private bool _isTypeKeySequence;
    [ObservableProperty] private bool _isTypeSystem;

    private ActionType SelectedType => IsTypeApplication ? ActionType.Application
        : IsTypeFolder ? ActionType.Folder
        : IsTypeUrl ? ActionType.Url
        : IsTypeCommand ? ActionType.Command
        : IsTypeSnippet ? ActionType.Snippet
        : IsTypeKeySequence ? ActionType.KeySequence
        : ActionType.System;

    private void SetTypeFlags(ActionType t)
    {
        IsTypeApplication = t == ActionType.Application;
        IsTypeFolder = t == ActionType.Folder;
        IsTypeUrl = t == ActionType.Url;
        IsTypeCommand = t == ActionType.Command;
        IsTypeSnippet = t == ActionType.Snippet;
        IsTypeKeySequence = t == ActionType.KeySequence;
        IsTypeSystem = t == ActionType.System;
        OnPropertyChanged(nameof(TargetLabel));
        OnPropertyChanged(nameof(SystemActionHint));
    }

    public void SetType(ActionType t) => SetTypeFlags(t);

    public string TargetLabel => SelectedType switch
    {
        ActionType.Application => "Program path",
        ActionType.Folder => "Folder path",
        ActionType.Url => "URL",
        ActionType.Command => "Command to run",
        ActionType.KeySequence => "Keys to send",
        _ => "Target",
    };

    public string SequenceHint =>
        "Type text literally — it's sent layout-independent via Unicode. Special keys go in braces: "
        + "{Enter}, {Tab}, {F5}, {Ctrl+C}, {Win+R}. Hold with {Shift down} … {Shift up}, wait with {Delay 250}, "
        + "escape a brace with {{.";

    public IReadOnlyList<string> QuickTokens { get; } = new[]
    {
        "{Tab}", "{Enter}", "{Ctrl+C}", "{Win+D}", "{Delay 250}",
    };

    // ---- fields ----
    [ObservableProperty] private string _name;
    [ObservableProperty] private string _description;
    [ObservableProperty] private string _category;
    [ObservableProperty] private string _targetText;
    [ObservableProperty] private string _arguments;
    [ObservableProperty] private string _workingDirectory;
    [ObservableProperty] private string _snippetText;
    [ObservableProperty] private string _sequenceText;
    [ObservableProperty] private int _systemActionIndex;
    [ObservableProperty] private string _hotkeyDisplay;
    [ObservableProperty] private string _emoji;

    partial void OnEmojiChanged(string value) => OnPropertyChanged(nameof(EmojiDisplay));
    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private bool _showToast;
    [ObservableProperty] private string _hotkeyWarning = "";

    public bool HasHotkeyWarning => !string.IsNullOrEmpty(HotkeyWarning);

    partial void OnSystemActionIndexChanged(int value)
    {
        if (SelectedType == ActionType.System)
            AutoFillEmoji();
    }

    partial void OnHotkeyDisplayChanged(string value)
    {
        ValidateHotkey();
        OnPropertyChanged(nameof(HasHotkeyDisplay));
    }

    public bool HasHotkeyDisplay => !string.IsNullOrEmpty(HotkeyDisplay);

    public string SystemActionHint =>
        SelectedType == ActionType.System && SystemActionInfo.All.Length > 0
            ? "Built-in: uses Windows API directly, no external program needed."
            : "";

    public string EmojiDisplay => string.IsNullOrEmpty(Emoji) ? "🎨" : Emoji;

    public sealed record SystemActionOption(SystemActionKind Kind, string Name, string Glyph);

    public IReadOnlyList<SystemActionOption> SystemActions { get; } =
        SystemActionInfo.All.Select(a => new SystemActionOption(a.Kind, a.Name, a.Glyph)).ToList();

    public IReadOnlyList<string> EmojiChoices { get; } = new[]
    {
        "🚀", "⚡", "📁", "🌐", "🖥️", "📋", "🎵", "🎮", "💻", "📧", "📝", "🔧",
        "🧩", "🛠️", "📊", "🧠", "⭐", "🔥", "🌙", "☀️", "🎨", "🍀", "💬", "🔒",
    };

    public bool CanSave =>
        !string.IsNullOrWhiteSpace(Name)
        && (SelectedType != ActionType.System || SystemActionInfo.All.Length > 0)
        && SelectedType switch
        {
            ActionType.Snippet => true,
            ActionType.System => true,
            ActionType.KeySequence => !string.IsNullOrWhiteSpace(SequenceText),
            _ => !string.IsNullOrWhiteSpace(TargetText),
        };

    public HotkeyGesture? CurrentGesture =>
        HotkeyGesture.TryParse(HotkeyDisplay.Replace(" + ", "+"), out var g) ? g : null;

    public void SetHotkey(HotkeyGesture g)
    {
        HotkeyDisplay = g.ToString();
    }

    public void ClearHotkey()
    {
        HotkeyDisplay = "";
        HotkeyWarning = "";
    }

    private void ValidateHotkey()
    {
        var g = CurrentGesture;
        if (g is null) { HotkeyWarning = ""; return; }
        var s = g.ToString();
        var clash = _existing.FirstOrDefault(x =>
            x.Id != Model.Id && x.Enabled && !string.IsNullOrEmpty(x.Hotkey) &&
            x.Gesture is { } og && og.ToString() == s);
        HotkeyWarning = clash is null ? "" : $"Already used by “{clash.Name}”";
        OnPropertyChanged(nameof(HasHotkeyWarning));
    }

    public void AutoFillEmoji()
    {
        Emoji = SelectedType == ActionType.System && SystemActionInfo.All.Length > 0
            ? SystemActionInfo.All[SystemActionIndex].Glyph
            : ShortcutAction.TypeEmoji(SelectedType);
    }

    public void ClearEmoji() => Emoji = "";

    public void RunTest()
    {
        _testRun?.Invoke();
    }

    /// <summary>Writes editor state back into a (new or existing) ShortcutAction.</summary>
    public ShortcutAction BuildModel(ShortcutAction target)
    {
        target.Name = Name.Trim();
        target.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        target.Category = string.IsNullOrWhiteSpace(Category) ? "General" : Category.Trim();
        target.Type = SelectedType;
        target.Arguments = SelectedType == ActionType.Application && !string.IsNullOrWhiteSpace(Arguments)
            ? Arguments : null;
        target.WorkingDirectory = SelectedType == ActionType.Application && !string.IsNullOrWhiteSpace(WorkingDirectory)
            ? WorkingDirectory : null;
        target.Target = SelectedType switch
        {
            ActionType.Snippet => SnippetText,
            ActionType.KeySequence => SequenceText,
            ActionType.System => SystemActionInfo.All.Length > 0
                ? SystemActionInfo.All[Math.Clamp(SystemActionIndex, 0, SystemActionInfo.All.Length - 1)].Kind.ToString()
                : "",
            _ => TargetText.Trim(),
        };
        target.Hotkey = CurrentGesture?.ToString();
        target.Emoji = string.IsNullOrWhiteSpace(Emoji) ? null : Emoji.Trim();
        target.Enabled = Enabled;
        target.ShowToast = ShowToast;
        return target;
    }
}

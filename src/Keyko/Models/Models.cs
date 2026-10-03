using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Avalonia.Input;

namespace Keyko.Models;

public enum ActionType
{
    Application,
    Folder,
    Url,
    Command,
    Snippet,
    KeySequence,
    System,
    Webhook,
    Script,
    Expansion
}

public enum ScheduleMode
{
    None,
    Interval,
    Daily
}

public sealed class ProfileSet
{
    public string Name { get; set; } = "Default";
    public List<ShortcutAction> Shortcuts { get; set; } = new();
}

public enum SystemActionKind
{
    // media & volume
    VolumeMute, VolumeUp, VolumeDown,
    MediaPlayPause, MediaNextTrack, MediaPreviousTrack, MediaStop,

    // panels & shells (Win combos)
    ClipboardHistory, QuickSettings, NotificationCenter, Widgets, SearchPanel,
    TaskView, CastPanel, ProjectPanel, StartMenu, GameBar,

    // display
    MonitorOff, ScreenshotTool, EmojiPanel,

    // power & session
    Lock, Sleep, Hibernate, Restart, ShutDown,

    // windows & apps
    TaskManager, ShowDesktop, MinimizeAll, RunDialog, ExplorerHome, SettingsApp,

    // shell places
    OpenDownloads, OpenDocuments, OpenPictures, OpenDesktopFolder, RecycleBin,

    // clipboard & input
    ClearClipboard, PasteAsPlainText, MicMute,

    // window management
    SnapLeft, SnapRight, SnapMaximize, AlwaysOnTop,
    TransparencyUp, TransparencyDown,
    VirtualDesktopNext, VirtualDesktopPrevious,
    MoveWindowLeft, MoveWindowRight,

    // tools
    ColorPicker, OcrRegion, CaseCycle, PauseKeyko,
}

public static class SystemActionInfo
{
    public static readonly (SystemActionKind Kind, string Name, string Glyph)[] All =
    {
        // media & volume
        (SystemActionKind.VolumeMute,         "Mute / unmute volume",      "\uE74F"),
        (SystemActionKind.VolumeUp,           "Volume up",                 "\uE767"),
        (SystemActionKind.VolumeDown,         "Volume down",               "\uE767"),
        (SystemActionKind.MediaPlayPause,     "Play / pause media",        "\uE768"),
        (SystemActionKind.MediaNextTrack,     "Next media track",          "\uE76B"),
        (SystemActionKind.MediaPreviousTrack, "Previous media track",      "\uE76C"),
        (SystemActionKind.MediaStop,          "Stop media",                "\uE71A"),

        // panels & shells
        (SystemActionKind.ClipboardHistory,   "Open clipboard history",    "\uE77F"),
        (SystemActionKind.QuickSettings,      "Open quick settings",       "\uE713"),
        (SystemActionKind.NotificationCenter, "Open notification center",  "\uE7E7"),
        (SystemActionKind.Widgets,            "Open widgets board",        "\uE8A5"),
        (SystemActionKind.SearchPanel,        "Open search panel",         "\uE721"),
        (SystemActionKind.TaskView,           "Open Task View",            "\uE7C4"),
        (SystemActionKind.CastPanel,          "Open cast panel",           "\uE7F4"),
        (SystemActionKind.ProjectPanel,       "Open project panel",        "\uE7F4"),
        (SystemActionKind.StartMenu,          "Open Start menu",           "\uE782"),
        (SystemActionKind.GameBar,            "Open Game Bar",             "\uE7FC"),

        // display
        (SystemActionKind.MonitorOff,         "Turn monitor off",          "\uE7F4"),
        (SystemActionKind.ScreenshotTool,     "Snipping overlay",          "\uE722"),
        (SystemActionKind.EmojiPanel,         "Open emoji panel",          "\uE76E"),

        // power & session
        (SystemActionKind.Lock,               "Lock the PC",               "\uE72E"),
        (SystemActionKind.Sleep,              "Put the PC to sleep",       "\uE708"),
        (SystemActionKind.Hibernate,          "Hibernate the PC",          "\uE708"),
        (SystemActionKind.Restart,            "Restart the PC",            "\uE72C"),
        (SystemActionKind.ShutDown,           "Shut the PC down",          "\uE7E8"),

        // windows & apps
        (SystemActionKind.TaskManager,        "Open Task Manager",         "\uE9D9"),
        (SystemActionKind.ShowDesktop,        "Show desktop",              "\uE7F4"),
        (SystemActionKind.MinimizeAll,        "Minimize all windows",      "\uE921"),
        (SystemActionKind.RunDialog,          "Open the Run dialog",       "\uE713"),
        (SystemActionKind.ExplorerHome,       "Open File Explorer",        "\uE8B7"),
        (SystemActionKind.SettingsApp,        "Open Windows Settings",     "\uE713"),

        // shell places
        (SystemActionKind.OpenDownloads,      "Open Downloads folder",     "\uE896"),
        (SystemActionKind.OpenDocuments,      "Open Documents folder",     "\uE8A5"),
        (SystemActionKind.OpenPictures,       "Open Pictures folder",      "\uE722"),
        (SystemActionKind.OpenDesktopFolder,  "Open Desktop folder",       "\uE8B7"),
        (SystemActionKind.RecycleBin,         "Open Recycle Bin",          "\uE74D"),

        // clipboard & input
        (SystemActionKind.ClearClipboard,     "Clear the clipboard",       "\uE74D"),
        (SystemActionKind.PasteAsPlainText,   "Paste as plain text",       "\uE77F"),
        (SystemActionKind.MicMute,            "Mute / unmute microphone",  "\uE720"),
    };

    public static string DisplayName(string? target) =>
        Enum.TryParse<SystemActionKind>(target, out var k) ? Name(k) : target ?? "";

    public static string Name(SystemActionKind k)
    {
        foreach (var a in All) if (a.Kind == k) return a.Name;
        return k.ToString();
    }

    public static string Glyph(SystemActionKind k)
    {
        foreach (var a in All) if (a.Kind == k) return a.Glyph;
        return "\uE945";
    }
}

public sealed class ShortcutAction
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Category { get; set; } = "General";
    public ActionType Type { get; set; } = ActionType.Application;
    public string Target { get; set; } = "";
    public string? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }
    public string? Hotkey { get; set; }
    public bool Enabled { get; set; } = true;
    public bool ShowToast { get; set; } = true;
    public string? Emoji { get; set; }
    public int RunCount { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // expansion
    public string? Abbreviation { get; set; }

    // webhook
    public string? HttpMethod { get; set; } = "POST";
    public string? Body { get; set; }

    // scheduling
    public ScheduleMode Schedule { get; set; } = ScheduleMode.None;
    public int ScheduleIntervalMinutes { get; set; } = 30;
    public string? ScheduleDailyTime { get; set; } = "09:00";
    public DateTime? LastFiredAt { get; set; }

    // per-app scoping (semicolon-separated exe names, without .exe)
    public string? OnlyInApps { get; set; }
    public string? NotInApps { get; set; }

    [JsonIgnore] public HotkeyGesture? Gesture => HotkeyGesture.TryParse(Hotkey, out var g) ? g : null;

    [JsonIgnore] public bool HasHotkey => Gesture is not null;

    [JsonIgnore]
    public string TargetSummary => Type switch
    {
        ActionType.Application => System.IO.Path.GetFileName(Target)
            + (string.IsNullOrWhiteSpace(Arguments) ? "" : " " + Arguments),
        ActionType.Snippet => "Paste: " + (Target.Length > 44 ? Target[..44] + "…" : Target),
        ActionType.KeySequence => "Keys: " + (Target.Length > 44 ? Target[..44] + "…" : Target),
        ActionType.System => SystemActionInfo.DisplayName(Target),
        ActionType.Webhook => "Webhook: " + (Target.Length > 40 ? Target[..40] + "…" : Target),
        ActionType.Script => "Script: " + (Target.Length > 40 ? Target[..40] + "…" : Target),
        ActionType.Expansion => "Expand: " + (Abbreviation ?? "") + " → " + (Target.Length > 30 ? Target[..30] + "…" : Target),
        _ => Target,
    };

    public static string TypeGlyph(ActionType t) => t switch
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
        _ => "\uE7C3",
    };
}

public sealed class AppSettings
{
    public string Theme { get; set; } = "Dark"; // Dark | Light
    public bool FollowSystemTheme { get; set; }
    public string Accent1 { get; set; } = "#F472B6";
    public string Accent2 { get; set; } = "#A78BFA";
    public double GlassTintOpacity { get; set; } = 0.72;
    public bool LaunchOnStartup { get; set; }
    public bool RunAsAdmin { get; set; }
    public bool StartMinimized { get; set; } = true;
    public bool ShowToasts { get; set; } = true;
    public bool SoundOnLaunch { get; set; }
    public string? SearchHotkey { get; set; } = "Ctrl+Alt+Space";
    public string? PauseHotkey { get; set; }
    public string? ProfileCycleHotkey { get; set; }
    public string ActiveProfile { get; set; } = "Default";
    public List<ProfileSet> Profiles { get; set; } = new();
    public List<string> GlobalAppExclusions { get; set; } = new();
    public List<ShortcutAction> Shortcuts { get; set; } = new();
}

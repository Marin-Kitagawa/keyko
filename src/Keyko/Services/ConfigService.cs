using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Keyko.Models;

namespace Keyko.Services;

/// <summary>
/// Loads/saves the JSON profile under %APPDATA%\Keyko (or KEYKO_CONFIG_DIR override).
/// </summary>
public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Dir { get; }
    public string FilePath { get; }
    public string IconsDir { get; }
    public AppSettings Settings { get; private set; } = new();

    public event Action? Saved;

    public ConfigService()
    {
        var root = Environment.GetEnvironmentVariable("KEYKO_CONFIG_DIR");
        if (string.IsNullOrWhiteSpace(root))
            root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Keyko");

        Dir = root;
        IconsDir = Path.Combine(Dir, "Icons");
        FilePath = Path.Combine(Dir, "config.json");
        Directory.CreateDirectory(Dir);
        Directory.CreateDirectory(IconsDir);
    }

    public void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                Settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOpts) ?? new AppSettings();
                return;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Keyko: failed to read config, using defaults. {ex.Message}");
        }

        Settings = SeedDefaults();
        Save();
    }

    public void Save()
    {
        try
        {
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Settings, JsonOpts));
            File.Move(tmp, FilePath, overwrite: true);
            Saved?.Invoke();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Keyko: failed to write config. {ex.Message}");
        }
    }

    private static AppSettings SeedDefaults() => new()
    {
        Shortcuts =
        {
            new ShortcutAction
            {
                Name = "File Explorer",
                Description = "Open your user folder",
                Category = "System",
                Type = ActionType.Folder,
                Target = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Hotkey = "Ctrl+Alt+E",
            },
            new ShortcutAction
            {
                Name = "Command Prompt",
                Description = "Classic terminal",
                Category = "Tools",
                Type = ActionType.Application,
                Target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
                Hotkey = "Ctrl+Alt+N",
            },
            new ShortcutAction
            {
                Name = "Google",
                Description = "Search the web",
                Category = "Web",
                Type = ActionType.Url,
                Target = "https://www.google.com",
                Hotkey = "Ctrl+Alt+G",
            },
            new ShortcutAction
            {
                Name = "Focus address bar",
                Description = "Selects the URL in the active browser",
                Category = "Web",
                Type = ActionType.KeySequence,
                Target = "{Ctrl+L}",
                Hotkey = "Ctrl+Alt+A",
            },
            new ShortcutAction
            {
                Name = "Mute volume",
                Description = "Toggle system mute",
                Category = "Media",
                Type = ActionType.System,
                Target = SystemActionKind.VolumeMute.ToString(),
                Hotkey = "Ctrl+Alt+M",
            },
        }
    };
}

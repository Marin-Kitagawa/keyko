using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Threading;

namespace Keyko.Services;

/// <summary>
/// Global runtime state: pause, app exclusions, current profile name.
/// Checked by the hotkey engine, expansion hook and scheduler.
/// </summary>
public static class KeykoState
{
    private static readonly object Gate = new();
    private static bool _paused;
    private static readonly List<string> Exclusions = new();

    /// <summary>When true, no hotkey or expansion fires.</summary>
    public static bool Paused
    {
        get { lock (Gate) return _paused; }
    }

    public static event Action<bool>? PauseChanged;

    public static void SetPaused(bool value)
    {
        bool changed;
        lock (Gate)
        {
            changed = _paused != value;
            _paused = value;
        }
        if (changed) PauseChanged?.Invoke(value);
    }

    public static void TogglePause() => SetPaused(!Paused);

    public static void SetExclusions(IEnumerable<string> apps)
    {
        lock (Gate)
        {
            Exclusions.Clear();
            Exclusions.AddRange(apps.Select(a => a.Trim().ToLowerInvariant()).Where(a => a.Length > 0));
        }
    }

    public static bool IsExcluded(string exeName)
    {
        lock (Gate) return Exclusions.Contains(exeName.ToLowerInvariant());
    }
}

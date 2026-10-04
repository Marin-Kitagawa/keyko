using System;
using System.Linq;
using Keyko.Models;
using Keyko.Services;

namespace Keyko.Interop;

/// <summary>
/// Decides whether a shortcut is allowed to fire right now, based on pause
/// state (checked by the caller), per-shortcut app scoping and the global
/// exclusion list. Exe names are compared without extension, case-insensitive.
/// </summary>
public static class KeykoDispatch
{
    public static bool AllowsOnForeground(ShortcutAction a)
    {
        var exe = ForegroundMonitor.CurrentExe;
        if (string.IsNullOrEmpty(exe)) return true;

        if (KeykoState.IsExcluded(exe)) return false;

        if (!string.IsNullOrWhiteSpace(a.OnlyInApps))
        {
            var allow = Split(a.OnlyInApps);
            if (allow.Length > 0 && !allow.Contains(exe)) return false;
        }

        if (!string.IsNullOrWhiteSpace(a.NotInApps) && Split(a.NotInApps).Contains(exe))
            return false;

        return true;
    }

    public static bool ExcludesForeground(System.Collections.Generic.IEnumerable<string> exclusions)
    {
        var exe = ForegroundMonitor.CurrentExe;
        return !string.IsNullOrEmpty(exe) && exclusions.Contains(exe);
    }

    private static string[] Split(string? list) =>
        (list ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? x[..^4] : x)
            .Select(x => x.ToLowerInvariant())
            .ToArray();
}

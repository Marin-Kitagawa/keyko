using System;
using System.IO;
using Keyko.Models;

namespace Keyko.Services;

/// <summary>
/// Extracts an icon from a target executable and caches it as PNG next to the config.
/// </summary>
public static class IconCacheService
{
    public static string? GetIconPath(ShortcutAction action, string iconsDir)
    {
        if (action.Type != ActionType.Application) return null;
        try
        {
            if (string.IsNullOrWhiteSpace(action.Target) || !File.Exists(action.Target)) return null;
            var dest = Path.Combine(iconsDir, action.Id + ".png");
            if (File.Exists(dest)) return dest;

            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(action.Target);
            if (icon is null) return null;
            using var bmp = icon.ToBitmap();
            bmp.Save(dest, System.Drawing.Imaging.ImageFormat.Png);
            return dest;
        }
        catch
        {
            return null;
        }
    }

    public static void Invalidate(string actionId, string iconsDir)
    {
        try
        {
            var p = Path.Combine(iconsDir, actionId + ".png");
            if (File.Exists(p)) File.Delete(p);
        }
        catch { /* non-fatal */ }
    }
}

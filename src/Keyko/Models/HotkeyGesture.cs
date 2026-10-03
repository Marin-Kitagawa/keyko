using System;
using Avalonia.Input;

namespace Keyko.Models;

/// <summary>
/// A serializable global hotkey combination, e.g. "Ctrl+Alt+K".
/// </summary>
public readonly record struct HotkeyGesture(Key Key, KeyModifiers Modifiers)
{
    public bool IsEmpty => Key == Key.None;

    public override string ToString() => Serialize(this);

    public string Display => ToString().Replace("+", " + ");

    public uint ToWin32Modifiers()
    {
        uint m = 0x4000; // MOD_NOREPEAT
        if (Modifiers.HasFlag(KeyModifiers.Control)) m |= 2;
        if (Modifiers.HasFlag(KeyModifiers.Alt)) m |= 1;
        if (Modifiers.HasFlag(KeyModifiers.Shift)) m |= 4;
        if (Modifiers.HasFlag(KeyModifiers.Meta)) m |= 8;
        return m;
    }

    public static string Serialize(HotkeyGesture g)
    {
        if (g.Key == Key.None) return "";
        var parts = new System.Collections.Generic.List<string>(5);
        if (g.Modifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (g.Modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (g.Modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (g.Modifiers.HasFlag(KeyModifiers.Meta)) parts.Add("Win");
        parts.Add(KeyName(g.Key));
        return string.Join("+", parts);
    }

    public static bool TryParse(string? s, out HotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(s)) return false;

        var modifiers = KeyModifiers.None;
        Key key = Key.None;
        var tokens = s.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0) return false;

        for (int i = 0; i < tokens.Length; i++)
        {
            var t = tokens[i];
            bool isLast = i == tokens.Length - 1;
            switch (t.ToLowerInvariant())
            {
                case "ctrl" or "control" when !isLast: modifiers |= KeyModifiers.Control; break;
                case "alt" when !isLast: modifiers |= KeyModifiers.Alt; break;
                case "shift" when !isLast: modifiers |= KeyModifiers.Shift; break;
                case "win" or "meta" or "windows" when !isLast: modifiers |= KeyModifiers.Meta; break;
                default:
                    if (!isLast) return false;
                    if (!Enum.TryParse(t, true, out key) || key == Key.None) return false;
                    break;
            }
        }

        if (key == Key.None) return false;
        gesture = new HotkeyGesture(key, modifiers);
        return true;
    }

    public static string KeyName(Key k) => k switch
    {
        >= Key.A and <= Key.Z => ((char)('A' + (k - Key.A))).ToString(),
        >= Key.D0 and <= Key.D9 => ((char)('0' + (k - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => "Num" + ((int)k - (int)Key.NumPad0),
        Key.Space => "Space",
        Key.Escape => "Esc",
        Key.Return => "Enter",
        _ => k.ToString(),
    };
}

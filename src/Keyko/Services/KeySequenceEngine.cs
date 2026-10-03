using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Keyko.Interop;

namespace Keyko.Services;

/// <summary>
/// Sends a key sequence via SendInput, Clavier+-style.
///
/// Syntax:
///   plain text is typed as-is (Unicode, layout-independent)
///   {Enter} {Tab} {Esc} {Backspace} {Delete} {Home} {End} {PgUp} {PgDn} {Up} {Left} {F5} ...
///   {Ctrl} {Alt} {Shift} {Win} {VolumeMute} {Next} {PlayPause}
///   {Ctrl+C} {Win+R} — hold modifiers, tap the last key, release
///   {Shift down} ... {Shift up} — hold a key across several tokens
///   {Delay 250} — pause in milliseconds
///   {{ — a literal brace
/// </summary>
public static class KeySequenceEngine
{
    private const ushort VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B;

    private static readonly Dictionary<string, ushort> Keys = new(StringComparer.OrdinalIgnoreCase);

    static KeySequenceEngine()
    {
        void Add(string name, ushort vk) => Keys[name] = vk;
        Add("ctrl", VK_CONTROL); Add("control", VK_CONTROL);
        Add("alt", VK_MENU);
        Add("shift", VK_SHIFT);
        Add("win", VK_LWIN); Add("windows", VK_LWIN);
        Add("enter", 0x0D); Add("return", 0x0D);
        Add("tab", 0x09);
        Add("esc", 0x1B); Add("escape", 0x1B);
        Add("space", 0x20);
        Add("backspace", 0x08); Add("back", 0x08);
        Add("delete", 0x2E); Add("del", 0x2E);
        Add("insert", 0x2D); Add("ins", 0x2D);
        Add("home", 0x24); Add("end", 0x23);
        Add("pageup", 0x21); Add("pgup", 0x21);
        Add("pagedown", 0x22); Add("pgdn", 0x22);
        Add("up", 0x26); Add("down", 0x28); Add("left", 0x25); Add("right", 0x27);
        Add("printscreen", 0x2C); Add("prtsc", 0x2C);
        Add("capslock", 0x14); Add("numlock", 0x90); Add("scrolllock", 0x91);
        Add("apps", 0x5D); Add("menu", 0x5D);
        Add("volumeup", 0xAF); Add("volumedown", 0xAE); Add("volumemute", 0xAD);
        Add("playpause", 0xB3); Add("play", 0xB3);
        Add("next", 0xB0); Add("prev", 0xB1); Add("previous", 0xB1);
        Add("add", 0x6B); Add("subtract", 0x6D); Add("multiply", 0x6A); Add("divide", 0x6F);
        for (int i = 1; i <= 24; i++) Keys[$"f{i}"] = (ushort)(0x70 + i - 1);
    }

    public static void Send(string sequence)
    {
        if (string.IsNullOrEmpty(sequence)) return;

        var batch = new List<Native.INPUT>(48);
        var held = new List<ushort>();

        void Flush()
        {
            if (batch.Count == 0) return;
            Native.SendInput((uint)batch.Count, batch.ToArray(), Marshal.SizeOf<Native.INPUT>());
            batch.Clear();
        }

        void Key(ushort vk, bool up)
        {
            var inp = new Native.INPUT { type = Native.INPUT_KEYBOARD };
            inp.U.ki.wVk = vk;
            inp.U.ki.dwFlags = up ? Native.KEYEVENTF_KEYUP : 0;
            batch.Add(inp);
            if (batch.Count >= 40) Flush();
        }

        void Uni(char c, bool up)
        {
            var inp = new Native.INPUT { type = Native.INPUT_KEYBOARD };
            inp.U.ki.wScan = (ushort)c;
            inp.U.ki.dwFlags = Native.KEYEVENTF_UNICODE | (up ? Native.KEYEVENTF_KEYUP : 0);
            batch.Add(inp);
            if (batch.Count >= 40) Flush();
        }

        void TypeCharWithModifiers(char c)
        {
            var res = Native.VkKeyScanW(c);
            if (res == -1)
            {
                // character isn't on the current layout: release what's held, type via Unicode, restore
                foreach (var hv in held) Key(hv, true);
                Flush();
                Uni(c, false); Uni(c, true);
                Flush();
                foreach (var hv in held) Key(hv, false);
                return;
            }

            ushort charVk = (ushort)(res & 0xFF);
            var shift = (sbyte)((res >> 8) & 0xFF);
            bool needShift = (shift & 1) != 0;
            bool needCtrl = (shift & 2) != 0;
            bool needAlt = (shift & 4) != 0;

            var pressed = new List<ushort>();
            if (needShift && !held.Contains(VK_SHIFT)) { Key(VK_SHIFT, false); pressed.Add(VK_SHIFT); }
            if (needCtrl && !held.Contains(VK_CONTROL)) { Key(VK_CONTROL, false); pressed.Add(VK_CONTROL); }
            if (needAlt && !held.Contains(VK_MENU)) { Key(VK_MENU, false); pressed.Add(VK_MENU); }
            Key(charVk, false); Key(charVk, true);
            for (int k = pressed.Count - 1; k >= 0; k--) Key(pressed[k], true);
            for (int k = pressed.Count - 1; k >= 0; k--) Key(pressed[k], true);
        }

        void TypeChar(char c)
        {
            if (held.Count > 0)
            {
                TypeCharWithModifiers(c);
                return;
            }
            Uni(c, false); Uni(c, true);
        }

        int i = 0;
        while (i < sequence.Length)
        {
            char c = sequence[i];

            if (c == '{')
            {
                if (i + 1 < sequence.Length && sequence[i + 1] == '{')
                {
                    TypeChar('{');
                    i += 2;
                    continue;
                }

                int close = sequence.IndexOf('}', i + 1);
                if (close < 0)
                {
                    TypeChar(c);
                    i++;
                    continue;
                }

                var token = sequence[(i + 1)..close].Trim();
                i = close + 1;
                if (token.Length == 0) continue;

                if (token.StartsWith("delay", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(token[5..].Trim(), out var ms) && ms > 0)
                    {
                        Flush();
                        System.Threading.Thread.Sleep(Math.Min(ms, 10_000));
                    }
                    continue;
                }

                bool isDown = false, isUp = false;
                var body = token;
                if (body.EndsWith(" down", StringComparison.OrdinalIgnoreCase)) { isDown = true; body = body[..^5].Trim(); }
                else if (body.EndsWith(" up", StringComparison.OrdinalIgnoreCase)) { isUp = true; body = body[..^3].Trim(); }

                var parts = body.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var vks = new List<ushort>();
                foreach (var p in parts)
                    if (Keys.TryGetValue(p, out var kv)) vks.Add(kv);
                if (vks.Count == 0) continue; // unknown token: skip silently

                if (isDown)
                {
                    foreach (var kv in vks)
                    {
                        Key(kv, false);
                        if (IsModifier(kv) && !held.Contains(kv)) held.Add(kv);
                    }
                }
                else if (isUp)
                {
                    foreach (var kv in vks)
                    {
                        Key(kv, true);
                        if (IsModifier(kv)) held.Remove(kv);
                    }
                }
                else if (vks.Count == 1)
                {
                    Key(vks[0], false); Key(vks[0], true);
                }
                else
                {
                    // {Ctrl+Shift+T}: hold everything but the last, tap the last, release
                    for (int k = 0; k < vks.Count - 1; k++) Key(vks[k], false);
                    Key(vks[^1], false); Key(vks[^1], true);
                    for (int k = vks.Count - 2; k >= 0; k--) Key(vks[k], true);
                }
                Flush();
            }
            else if (c == '}')
            {
                TypeChar(c);
                i++;
            }
            else if (char.IsHighSurrogate(c) && i + 1 < sequence.Length && char.IsLowSurrogate(sequence[i + 1]))
            {
                if (held.Count == 0)
                {
                    // both downs before the ups so Windows combines the pair
                    Uni(c, false); Uni(sequence[i + 1], false);
                    Uni(sequence[i + 1], true); Uni(c, true);
                }
                else
                {
                    TypeChar(c); TypeChar(sequence[i + 1]);
                }
                i += 2;
            }
            else
            {
                TypeChar(c);
                i++;
            }
        }

        Flush();
        foreach (var vk in held) Key(vk, true);
        Flush();
    }

    private static bool IsModifier(ushort vk) => vk is VK_SHIFT or VK_CONTROL or VK_MENU or VK_LWIN;
}

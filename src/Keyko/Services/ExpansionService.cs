using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Threading;
using Keyko.Interop;
using Keyko.Models;

namespace Keyko.Services;

/// <summary>
/// Text expansion: a low-level keyboard hook watches typed characters and
/// replaces abbreviations with their replacement text as you type.
/// Respects pause mode, app exclusions and per-shortcut app scoping.
/// </summary>
public sealed class ExpansionService : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ShortcutAction> _expansions = new(StringComparer.Ordinal);
    private readonly StringBuilder _buffer = new();
    private IntPtr _hook;
    private Native.LowLevelKeyboardProc? _proc;
    private bool _enabled = true;
    private bool _disposed;

    public void Start()
    {
        if (!OperatingSystem.IsWindows() || _hook != IntPtr.Zero) return;

        _proc = HookProc;
        _hook = Native.SetWindowsHookExW(Native.WH_KEYBOARD_LL, _proc, Native.GetModuleHandleW(null), 0);
        if (_hook == IntPtr.Zero) return;

        // the hook needs a message pump on its installing thread
        var pump = new System.Threading.Thread(() =>
        {
            while (Native.GetMessageW(out var msg, IntPtr.Zero, 0, 0))
                Native.DispatchMessageW(ref msg);
        })
        { IsBackground = true, Name = "Keyko.ExpansionHook" };
        pump.Start();
    }

    /// <summary>Refresh the abbreviation set from the current shortcut list.</summary>
    public void SetExpansions(IEnumerable<ShortcutAction> actions)
    {
        lock (_gate)
        {
            _expansions.Clear();
            foreach (var a in actions)
            {
                if (!a.Enabled || a.Type != ActionType.Expansion) continue;
                var abbrev = a.Abbreviation?.Trim();
                if (string.IsNullOrEmpty(abbrev)) continue;
                _expansions[abbrev] = a;
            }
        }
    }

    public void SetEnabled(bool value) => _enabled = value;

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

        var msg = wParam.ToInt64();
        if (msg is not (Native.WM_KEYDOWN_ or Native.WM_SYSKEYDOWN_))
            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

        var vk = Marshal.ReadInt32(lParam); // KBDLLHOOKSTRUCT.vkCode
        var kbd = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);

        // never interfere when paused or excluded, or when a modifier is alone
        if (KeykoState.Paused || KeykoState.IsExcluded(ForegroundMonitor.CurrentExe))
        {
            _buffer.Clear();
            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        if (vk is >= 0xA0 and <= 0xA5 || vk is 0x5B or 0x5C) // lone modifiers
            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

        if (vk == 0x08) // backspace erases from the buffer
        {
            if (_buffer.Length > 0) _buffer.Length--;
            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        // non-printable resets the word
        if (vk is < 0x20 or > 0x5A && vk is not (0xBA or 0xBB or 0xBC or 0xBD or 0xBE or 0xBF or 0xC0 or 0xDB or 0xDC or 0xDD or 0xDE))
        {
            _buffer.Clear();
            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        char ch = vk switch
        {
            >= 0x41 and <= 0x5A => (char)('a' + (vk - 0x41)),
            >= 0x30 and <= 0x39 => (char)('0' + (vk - 0x30)),
            0x20 => ' ',
            0xBA => ';',
            0xBB => '=',
            0xBC => ',',
            0xBD => '-',
            0xBE => '.',
            0xBF => '/',
            0xC0 => '`',
            0xDB => '[',
            0xDC => '\\',
            0xDD => ']',
            0xDE => '\'',
            _ => (char)vk,
        };
        if ((kbd.flags & 0x1) != 0 && char.IsLetter(ch)) ch = char.ToUpperInvariant(ch); // shift

        _buffer.Append(ch);
        if (_buffer.Length > 64) _buffer.Remove(0, _buffer.Length - 64);

        // find the longest matching abbreviation at the buffer tail
        ShortcutAction? match = null;
        int matchLen = 0;
        lock (_gate)
        {
            foreach (var (abbrev, action) in _expansions)
            {
                if (abbrev.Length <= matchLen) continue;
                if (_buffer.Length < abbrev.Length) continue;
                if (string.CompareOrdinal(_buffer.ToString(_buffer.Length - abbrev.Length, abbrev.Length), abbrev) == 0)
                {
                    match = action;
                    matchLen = abbrev.Length;
                }
            }
        }
        if (match is null)
            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

        // word boundary: the char before the abbreviation must be whitespace (or buffer start)
        if (_buffer.Length > matchLen && !char.IsWhiteSpace(_buffer[_buffer.Length - matchLen - 1]))
            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

        // consume the abbreviation keystrokes and inject the replacement
        _buffer.Clear();
        if (matchLen > 1)
        {
            // one backspace press per remaining abbreviation character, as down/up pairs
            var backs2 = new Native.INPUT[(matchLen - 1) * 2];
            for (int i = 0; i < matchLen - 1; i++)
            {
                backs2[i * 2].type = Native.INPUT_KEYBOARD;
                backs2[i * 2].U.ki.wVk = 0x08;
                backs2[i * 2 + 1].type = Native.INPUT_KEYBOARD;
                backs2[i * 2 + 1].U.ki.wVk = 0x08;
                backs2[i * 2 + 1].U.ki.dwFlags = Native.KEYEVENTF_KEYUP;
            }
            Native.SendInput((uint)backs2.Length, backs2, Marshal.SizeOf<Native.INPUT>());
        }

        // wait a beat, then type the replacement as unicode
        System.Threading.Thread.Sleep(30);
        var text = match.Target;
        var chars = new System.Collections.Generic.List<Native.INPUT>(text.Length * 2);
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsSurrogatePair(text, i))
            {
                chars.Add(Uni(text[i], false));
                chars.Add(Uni(text[i + 1], false));
                chars.Add(Uni(text[i + 1], true));
                chars.Add(Uni(text[i], true));
                i++;
            }
            else
            {
                chars.Add(Uni(text[i], false));
                chars.Add(Uni(text[i], true));
            }
        }
        var arr = chars.ToArray();
        Native.SendInput((uint)arr.Length, arr, Marshal.SizeOf<Native.INPUT>());

        return new IntPtr(1); // swallow the final keystroke
    }

    private static Native.INPUT Uni(char c, bool up)
    {
        var i = new Native.INPUT { type = Native.INPUT_KEYBOARD };
        i.U.ki.wScan = (ushort)c;
        i.U.ki.dwFlags = Native.KEYEVENTF_UNICODE | (up ? Native.KEYEVENTF_KEYUP : 0);
        return i;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_hook != IntPtr.Zero) Native.UnhookWindowsHookEx(_hook);
        Native.PostQuitMessage(0);
    }
}

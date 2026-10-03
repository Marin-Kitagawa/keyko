using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia.Input;
using Avalonia.Threading;
using Keyko.Interop;
using Keyko.Models;

namespace Keyko.Services;

/// <summary>
/// Registers system-wide hotkeys at THREAD level (RegisterHotKey with a NULL window).
/// WM_HOTKEY is posted to the queue of the thread that called RegisterHotKey, so the
/// apply command is marshalled onto the dedicated hotkey thread — registration, the
/// message pump and cleanup all run there. No helper window is involved, which removes
/// the window-class failure mode entirely. Survives the main window hiding to the tray.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private Thread? _thread;
    private uint _threadId;
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly object _gate = new();
    private List<ShortcutAction>? _pending;
    private readonly Dictionary<int, ShortcutAction> _map = new();
    private int _nextId = 1;
    private bool _disposed;

    /// <summary>Fired on the UI thread with the number of hotkeys successfully armed.</summary>
    public event Action<int>? Armed;

    /// <summary>Fired on the UI thread with shortcuts whose combos another app already owns.</summary>
    public event Action<IReadOnlyList<ShortcutAction>>? Conflicts;

    /// <summary>Fired on the UI thread when a registered hotkey is pressed.</summary>
    public event Action<ShortcutAction>? HotkeyPressed;

    public HotkeyService()
    {
        if (!OperatingSystem.IsWindows())
            return; // hotkeys are Windows-only today; other platforms run without them

        _thread = new Thread(MessageLoop) { IsBackground = true, Name = "Keyko.Hotkeys" };
        _thread.Start();
        _ready.Wait(TimeSpan.FromSeconds(3));
    }

    private void MessageLoop()
    {
        _threadId = Native.GetCurrentThreadId();
        _ready.Set();

        while (Native.GetMessageW(out var msg, IntPtr.Zero, 0, 0))
        {
            if (msg.message == Native.WM_APP_APPLY)
            {
                ApplyOnThisThread();
            }
            else if (msg.message == Native.WM_HOTKEY)
            {
                ShortcutAction? action;
                lock (_gate) { _map.TryGetValue((int)msg.wParam, out action); }
                if (action != null)
                    Dispatcher.UIThread.Post(() => HotkeyPressed?.Invoke(action));
            }
        }
    }

    private void ApplyOnThisThread()
    {
        List<ShortcutAction> actions;
        lock (_gate)
        {
            if (_pending is null) return;
            actions = _pending;
            _pending = null;
        }

        var registered = 0;
        var failed = new List<ShortcutAction>();

        lock (_gate)
        {
            foreach (var id in _map.Keys) Native.UnregisterHotKey(IntPtr.Zero, id);
            _map.Clear();
            _nextId = 1;

            foreach (var a in actions)
            {
                if (!a.Enabled || a.Gesture is not { } g || g.IsEmpty) continue;
                var vk = KeyToVk(g.Key);
                if (vk != 0 && Native.RegisterHotKey(IntPtr.Zero, _nextId, g.ToWin32Modifiers(), vk))
                {
                    _map[_nextId++] = a;
                    registered++;
                }
                else
                {
                    failed.Add(a);
                }
            }
        }

        Dispatcher.UIThread.Post(() =>
        {
            Armed?.Invoke(registered);
            if (failed.Count > 0) Conflicts?.Invoke(failed);
        });
    }

    /// <summary>(Re)registers every enabled shortcut. Registration happens on the hotkey
    /// thread; results arrive via the Armed / Conflicts events.</summary>
    public void Apply(IEnumerable<ShortcutAction> actions)
    {
        if (_disposed || !OperatingSystem.IsWindows()) return;
        if (!_ready.IsSet) _ready.Wait(TimeSpan.FromSeconds(3));
        if (_threadId == 0) return;

        lock (_gate) { _pending = new List<ShortcutAction>(actions); }
        if (!Native.PostThreadMessageW(_threadId, Native.WM_APP_APPLY, IntPtr.Zero, IntPtr.Zero))
        {
            // queue died (thread exited) — retry once from scratch
            lock (_gate) { _pending = new List<ShortcutAction>(actions); }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Native.PostQuitMessage(0);
        _ready.Dispose();
    }

    /// <summary>Avalonia Key → Win32 virtual-key code (covers everything a hotkey can reasonably use).</summary>
    private static uint KeyToVk(Key k) => k switch
    {
        >= Key.A and <= Key.Z => (uint)('A' + (k - Key.A)),
        >= Key.D0 and <= Key.D9 => (uint)('0' + (k - Key.D0)),
        >= Key.NumPad0 and <= Key.NumPad9 => (uint)(0x60 + (k - Key.NumPad0)),
        >= Key.F1 and <= Key.F24 => (uint)(0x70 + (k - Key.F1)),
        Key.Space => 0x20,
        Key.Return => 0x0D,
        Key.Escape => 0x1B,
        Key.Back => 0x08,
        Key.Tab => 0x09,
        Key.CapsLock => 0x14,
        Key.Insert => 0x2D,
        Key.Delete => 0x2E,
        Key.Home => 0x24,
        Key.End => 0x23,
        Key.PageUp => 0x21,
        Key.PageDown => 0x22,
        Key.Left => 0x25,
        Key.Up => 0x26,
        Key.Right => 0x27,
        Key.Down => 0x28,
        Key.Pause => 0x13,
        Key.Scroll => 0x91,
        Key.PrintScreen => 0x2C,
        Key.OemTilde => 0xC0,
        Key.OemMinus => 0xBD,
        Key.OemPlus => 0xBB,
        Key.OemOpenBrackets => 0xDB,
        Key.OemCloseBrackets => 0xDD,
        Key.OemPipe => 0xDC,
        Key.OemSemicolon => 0xBA,
        Key.OemQuotes => 0xDE,
        Key.OemComma => 0xBC,
        Key.OemPeriod => 0xBE,
        Key.OemQuestion => 0xBF,
        Key.OemBackslash => 0xE2,
        Key.Add => 0x6B,
        Key.Subtract => 0x6D,
        Key.Multiply => 0x6A,
        Key.Divide => 0x6F,
        Key.Decimal => 0x6E,
        Key.Clear => 0x0C,
        _ => 0,
    };
}

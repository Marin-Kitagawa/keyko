using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Keyko.Interop;
using Keyko.Models;

namespace Keyko.Services;

public static class ActionRunner
{
    public static async Task<(bool Ok, string? Error)> RunAsync(ShortcutAction a, Window clipboardHost)
    {
        try
        {
            switch (a.Type)
            {
                case ActionType.Application:
                    Process.Start(new ProcessStartInfo(a.Target)
                    {
                        Arguments = a.Arguments ?? "",
                        WorkingDirectory = string.IsNullOrWhiteSpace(a.WorkingDirectory) ? "" : a.WorkingDirectory,
                        UseShellExecute = true,
                    });
                    break;

                case ActionType.Folder:
                    Process.Start(new ProcessStartInfo(a.Target) { UseShellExecute = true });
                    break;

                case ActionType.Url:
                    Process.Start(new ProcessStartInfo(a.Target) { UseShellExecute = true });
                    break;

                case ActionType.Command:
                    Process.Start(new ProcessStartInfo("cmd.exe")
                    {
                        Arguments = "/c " + a.Target,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    });
                    break;

                case ActionType.Snippet:
                    if (clipboardHost.Clipboard is null) return (false, "Clipboard unavailable");
                    await clipboardHost.Clipboard.SetTextAsync(a.Target);

                    // verify the write actually landed (clipboard managers can race it away)
                    var readBack = await clipboardHost.Clipboard.GetTextAsync();
                    if (!string.Equals(readBack, a.Target, StringComparison.Ordinal))
                    {
                        await Task.Delay(120);
                        await clipboardHost.Clipboard.SetTextAsync(a.Target);
                        readBack = await clipboardHost.Clipboard.GetTextAsync();
                        if (!string.Equals(readBack, a.Target, StringComparison.Ordinal))
                            return (false, "Another app is holding the clipboard");
                    }

                    // the hotkey's physical modifiers may still be down — pasting now
                    // would send Ctrl+Alt+V instead of Ctrl+V. Wait for release off-thread.
                    await Task.Run(() => WaitForModifierRelease());
                    SendPaste();
                    break;

                case ActionType.KeySequence:
                    await Task.Run(() =>
                    {
                        WaitForModifierRelease();
                        KeySequenceEngine.Send(a.Target);
                    });
                    break;

                case ActionType.System:
                    await Task.Run(() =>
                        DoSystemAction(Enum.TryParse<SystemActionKind>(a.Target, out var k) ? k : SystemActionKind.VolumeMute));
                    break;
            }
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// WM_HOTKEY fires while the user is still holding the hotkey's physical modifiers.
    /// Any synthesized input sent before they're released arrives modified
    /// (Ctrl+Alt+V instead of Ctrl+V, Ctrl+Alt+Win+K instead of Win+K, …).
    /// Polls GetAsyncKeyState until all modifiers are up (bounded).
    /// </summary>
    private static void WaitForModifierRelease(int timeoutMs = 1600)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            bool down(int vk) => (Native.GetAsyncKeyState(vk) & 0x8000) != 0;
            if (!down(Native.VK_CONTROL) && !down(Native.VK_MENU) && !down(Native.VK_SHIFT)
                && !down(Native.VK_LWIN) && !down(Native.VK_RWIN))
            {
                System.Threading.Thread.Sleep(50); // let the last keyup finish propagating
                return;
            }
            System.Threading.Thread.Sleep(25);
        }
    }

    private static void SendPaste()
    {
        var inputs = new Native.INPUT[4];
        inputs[0].type = Native.INPUT_KEYBOARD;
        inputs[0].U.ki.wVk = Native.VK_CONTROL;
        inputs[1].type = Native.INPUT_KEYBOARD;
        inputs[1].U.ki.wVk = Native.VK_V;
        inputs[2].type = Native.INPUT_KEYBOARD;
        inputs[2].U.ki.wVk = Native.VK_V;
        inputs[2].U.ki.dwFlags = Native.KEYEVENTF_KEYUP;
        inputs[3].type = Native.INPUT_KEYBOARD;
        inputs[3].U.ki.wVk = Native.VK_CONTROL;
        inputs[3].U.ki.dwFlags = Native.KEYEVENTF_KEYUP;
        Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Native.INPUT>());
    }

    private static void SendWinCombo(ushort vk, bool shift = false, bool ctrl = false, bool alt = false)
    {
        var inputs = new List<Native.INPUT>();

        // panel combos (Win+V, Win+Shift+S, …) fail the same way snippets do if the
        // hotkey's physical Win key is still held — wait for it to come up
        WaitForModifierRelease();

        void Key(ushort key, bool up)
        {
            var i = new Native.INPUT { type = Native.INPUT_KEYBOARD };
            i.U.ki.wVk = key;
            i.U.ki.dwFlags = up ? Native.KEYEVENTF_KEYUP : 0;
            inputs.Add(i);
        }

        Key(Native.VK_LWIN, false);
        if (ctrl) Key(Native.VK_CONTROL, false);
        if (shift) Key(0x10, false);
        if (alt) Key(Native.VK_MENU, false);
        Key(vk, false);
        Key(vk, true);
        if (alt) Key(Native.VK_MENU, true);
        if (shift) Key(0x10, true);
        if (ctrl) Key(Native.VK_CONTROL, true);
        Key(Native.VK_LWIN, true);

        Native.SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<Native.INPUT>());
    }

    private static void TapKey(ushort vk)
    {
        Native.keybd_event((byte)vk, 0, 0, UIntPtr.Zero);
        Native.keybd_event((byte)vk, 0, Native.KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    private static void OpenShell(string target)
    {
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    }

    private static void DoSystemAction(SystemActionKind k)
    {
        switch (k)
        {
            // media & volume
            case SystemActionKind.VolumeMute: TapKey(Native.VK_VOLUME_MUTE); break;
            case SystemActionKind.VolumeUp: TapKey(Native.VK_VOLUME_UP); break;
            case SystemActionKind.VolumeDown: TapKey(Native.VK_VOLUME_DOWN); break;
            case SystemActionKind.MediaPlayPause: TapKey(Native.VK_MEDIA_PLAY_PAUSE); break;
            case SystemActionKind.MediaNextTrack: TapKey(Native.VK_MEDIA_NEXT_TRACK); break;
            case SystemActionKind.MediaPreviousTrack: TapKey(Native.VK_MEDIA_PREV_TRACK); break;
            case SystemActionKind.MediaStop: TapKey(0xB2); break;

            // panels & shells
            case SystemActionKind.ClipboardHistory: SendWinCombo(0x56); break;   // Win+V
            case SystemActionKind.QuickSettings: SendWinCombo(0x41); break;      // Win+A
            case SystemActionKind.NotificationCenter: SendWinCombo(0x4E); break; // Win+N
            case SystemActionKind.Widgets: SendWinCombo(0x57); break;            // Win+W
            case SystemActionKind.SearchPanel: SendWinCombo(0x53); break;        // Win+S
            case SystemActionKind.TaskView: SendWinCombo(0x09); break;           // Win+Tab
            case SystemActionKind.CastPanel: SendWinCombo(0x4B); break;          // Win+K
            case SystemActionKind.ProjectPanel: SendWinCombo(0x50); break;       // Win+P
            case SystemActionKind.StartMenu: TapKey(Native.VK_LWIN); break;
            case SystemActionKind.GameBar: SendWinCombo(0x47); break;            // Win+G

            // display
            case SystemActionKind.MonitorOff:
                Native.SendMessageW(Native.HWND_BROADCAST, Native.WM_SYSCOMMAND, Native.SC_MONITORPOWER, new IntPtr(2));
                break;
            case SystemActionKind.ScreenshotTool: SendWinCombo(0x53, shift: true); break; // Win+Shift+S
            case SystemActionKind.EmojiPanel: SendWinCombo(0xBB); break;         // Win+.

            // power & session
            case SystemActionKind.Lock: Native.LockWorkStation(); break;
            case SystemActionKind.Sleep: Native.SetSuspendState(false, false, false); break;
            case SystemActionKind.Hibernate: Native.SetSuspendState(true, false, false); break;
            case SystemActionKind.Restart:
                Process.Start(new ProcessStartInfo("shutdown", "/r /t 0") { CreateNoWindow = true, UseShellExecute = false });
                break;
            case SystemActionKind.ShutDown:
                Process.Start(new ProcessStartInfo("shutdown", "/s /t 0") { CreateNoWindow = true, UseShellExecute = false });
                break;

            // windows & apps
            case SystemActionKind.TaskManager:
                Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });
                break;
            case SystemActionKind.ShowDesktop: SendWinCombo(0x44); break;        // Win+D
            case SystemActionKind.MinimizeAll: SendWinCombo(0x4D); break;        // Win+M
            case SystemActionKind.RunDialog: SendWinCombo(0x52); break;          // Win+R
            case SystemActionKind.ExplorerHome: SendWinCombo(0x45); break;       // Win+E
            case SystemActionKind.SettingsApp: SendWinCombo(0x49); break;        // Win+I

            // shell places
            case SystemActionKind.OpenDownloads: OpenShell("shell:Downloads"); break;
            case SystemActionKind.OpenDocuments: OpenShell("shell:Personal"); break;
            case SystemActionKind.OpenPictures: OpenShell("shell:Pictures"); break;
            case SystemActionKind.OpenDesktopFolder: OpenShell("shell:Desktop"); break;
            case SystemActionKind.RecycleBin: OpenShell("shell:RecycleBinFolder"); break;

            // clipboard & input
            case SystemActionKind.ClearClipboard:
                if (Native.OpenClipboard(IntPtr.Zero))
                {
                    Native.EmptyClipboard();
                    Native.CloseClipboard();
                }
                break;
            case SystemActionKind.PasteAsPlainText: SendWinCombo(0x56, ctrl: true); break; // Win+Ctrl+V (Win11 paste as plain text)
            case SystemActionKind.MicMute: SendWinCombo(0x4B, alt: true); break; // Win+Alt+K

            // window management
            case SystemActionKind.SnapLeft: SnapFocused(left: true); break;
            case SystemActionKind.SnapRight: SnapFocused(left: false); break;
            case SystemActionKind.SnapMaximize: ToggleMaximizeFocused(); break;
            case SystemActionKind.AlwaysOnTop: ToggleTopmostFocused(); break;
            case SystemActionKind.TransparencyUp: AdjustTransparencyFocused(-32); break;
            case SystemActionKind.TransparencyDown: AdjustTransparencyFocused(+32); break;
            case SystemActionKind.VirtualDesktopNext: SendWinCombo(0x27, ctrl: true); break;      // Ctrl+Win+Right
            case SystemActionKind.VirtualDesktopPrevious: SendWinCombo(0x25, ctrl: true); break; // Ctrl+Win+Left
            case SystemActionKind.MoveWindowLeft: SendWinCombo(0x25, shift: true); break;        // Win+Shift+Left
            case SystemActionKind.MoveWindowRight: SendWinCombo(0x27, shift: true); break;       // Win+Shift+Right

            // tools
            case SystemActionKind.ColorPicker: Overlays.ShowColorPicker(); break;
            case SystemActionKind.OcrRegion: Overlays.ShowOcrRegion(); break;
            case SystemActionKind.CaseCycle: CycleSelectionCase(); break;
            case SystemActionKind.PauseKeyko: KeykoState.TogglePause(); break;
        }
    }

    private static void SnapFocused(bool left)
    {
        try
        {
            var hwnd = Native.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;
            var mon = Native.MonitorFromWindow(hwnd, Native.MONITOR_DEFAULTTONEAREST);
            var mi = new Native.MONITORINFO { cbSize = Marshal.SizeOf<Native.MONITORINFO>() };
            if (!Native.GetMonitorInfoW(mon, ref mi)) return;
            var wa = mi.rcWork;
            var w = (wa.Right - wa.Left) / 2;
            Native.SetWindowPos(hwnd, IntPtr.Zero,
                left ? wa.Left : wa.Left + w, wa.Top, w, wa.Bottom - wa.Top, 0x0004 /*NOZORDER*/ | 0x0010);
        }
        catch { }
    }

    private static void ToggleMaximizeFocused()
    {
        try
        {
            var hwnd = Native.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;
            var placement = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
            if (!GetWindowPlacement(hwnd, ref placement)) return;
            ShowWindow(hwnd, placement.showCmd == SW_SHOWMAXIMIZED ? SW_RESTORE : SW_SHOWMAXIMIZED);
        }
        catch { }
    }

    private static void ToggleTopmostFocused()
    {
        try
        {
            var hwnd = Native.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;
            var ex = Native.GetWindowLongPtr(hwnd, (int)Native.GWL_EXSTYLE).ToInt64();
            bool topmost = (ex & 0x8) != 0; // WS_EX_TOPMOST
            Native.SetWindowPos(hwnd, topmost ? Native.HWND_NOTOPMOST : Native.HWND_TOPMOST,
                0, 0, 0, 0, 0x0001 /*NOSIZE*/ | 0x0002 /*NOMOVE*/ | 0x0010 /*NOACTIVATE*/);
        }
        catch { }
    }

    private static readonly Dictionary<IntPtr, byte> AlphaByWindow = new();

    private static void AdjustTransparencyFocused(int delta)
    {
        try
        {
            var hwnd = Native.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;
            var ex = Native.GetWindowLongPtr(hwnd, (int)Native.GWL_EXSTYLE).ToInt64();
            if ((ex & Native.WS_EX_LAYERED) == 0)
            {
                Native.SetWindowLongPtr(hwnd, (int)Native.GWL_EXSTYLE, new IntPtr(ex | (long)Native.WS_EX_LAYERED));
                AlphaByWindow[hwnd] = 255;
            }
            var alpha = AlphaByWindow.TryGetValue(hwnd, out var a) ? a : (byte)255;
            int next = Math.Clamp(alpha + delta, 120, 255);
            AlphaByWindow[hwnd] = (byte)next;
            Native.SetLayeredWindowAttributes(hwnd, 0, (byte)next, Native.LWA_ALPHA);
        }
        catch { }
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT placement);

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPLACEMENT
    {
        public int length, flags, showCmd;
        public int ptMinX, ptMinY, ptMaxX, ptMaxY;
        public int rcNormalL, rcNormalT, rcNormalR, rcNormalB;
    }

    private const int SW_SHOWMAXIMIZED = 3;
    private const int SW_RESTORE = 9;

    /// <summary>Cycles the case of the selected text: save clipboard, select-copy, transform, paste.</summary>
    private static void CycleSelectionCase()
    {
        WaitForModifierRelease();
        var saved = ClipboardText.GetText();

        SendPaste(0x11); // Ctrl+C on the selection
        System.Threading.Thread.Sleep(220);
        var text = ClipboardText.GetText();
        if (string.IsNullOrEmpty(text)) return;

        var cycled = text switch
        {
            var s when s == s.ToUpperInvariant() && s != s.ToLowerInvariant() => s.ToLowerInvariant(),
            var s when s == s.ToLowerInvariant() => char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant(),
            var s => s.ToUpperInvariant(),
        };
        if (cycled == text) return;

        ClipboardText.SetText(cycled);
        System.Threading.Thread.Sleep(140);
        SendPaste();
        System.Threading.Thread.Sleep(150);
        ClipboardText.SetText(saved); // restore what the user had
    }

    /// <summary>Paste with a non-default modifier (e.g. Ctrl+C to capture a selection).</summary>
    private static void SendPaste(ushort modifier)
    {
        var inputs = new Native.INPUT[4];
        inputs[0].type = Native.INPUT_KEYBOARD;
        inputs[0].U.ki.wVk = modifier;
        inputs[1].type = Native.INPUT_KEYBOARD;
        inputs[1].U.ki.wVk = Native.VK_V;
        inputs[2].type = Native.INPUT_KEYBOARD;
        inputs[2].U.ki.wVk = Native.VK_V;
        inputs[2].U.ki.dwFlags = Native.KEYEVENTF_KEYUP;
        inputs[3].type = Native.INPUT_KEYBOARD;
        inputs[3].U.ki.wVk = modifier;
        inputs[3].U.ki.dwFlags = Native.KEYEVENTF_KEYUP;
        Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Native.INPUT>());
    }
}

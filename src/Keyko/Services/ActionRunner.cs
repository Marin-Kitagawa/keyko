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
                    await Task.Delay(160);
                    SendPaste();
                    break;

                case ActionType.KeySequence:
                    await Task.Run(() => KeySequenceEngine.Send(a.Target));
                    break;

                case ActionType.System:
                    DoSystemAction(Enum.TryParse<SystemActionKind>(a.Target, out var k) ? k : SystemActionKind.VolumeMute);
                    break;
            }
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
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
        }
    }
}

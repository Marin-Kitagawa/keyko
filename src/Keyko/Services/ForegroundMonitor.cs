using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Threading;
using Keyko.Interop;

namespace Keyko.Services;

/// <summary>
/// Tracks the foreground window's process name and title on a background timer.
/// Powers per-app shortcut scoping, auto-pause and the Insights page.
/// </summary>
public static class ForegroundMonitor
{
    private static readonly object Gate = new();
    private static System.Threading.Timer? _timer;
    private static string _exe = "";
    private static string _title = "";

    /// <summary>Lowercase exe name without extension (e.g. "chrome").</summary>
    public static string CurrentExe
    {
        get { lock (Gate) return _exe; }
    }

    public static string CurrentTitle
    {
        get { lock (Gate) return _title; }
    }

    /// <summary>Fired on a background thread when the foreground app changes.</summary>
    public static event Action<string, string>? ForegroundChanged;

    public static void Start()
    {
        if (_timer is not null) return;
        _timer = new System.Threading.Timer(_ => Poll(), null, 0, 600);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT2 { public int L, T, R, B; }

    private static void Poll()
    {
        try
        {
            var hwnd = Native.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;

            var pid = 0u;
            Native.GetWindowThreadProcessId(hwnd, out pid);
            if (pid == 0) return;

            var exe = "";
            var hProc = Native.OpenProcess(Native.PROCESS_QUERY_INFORMATION | Native.PROCESS_VM_READ, false, pid);
            if (hProc != IntPtr.Zero)
            {
                var sb = new StringBuilder(1024);
                if (Native.GetModuleFileNameEx(hProc, IntPtr.Zero, sb, 1024))
                    exe = sb.ToString();
                Native.CloseHandle(hProc);
            }
            if (exe.Length == 0) return;

            var name = System.IO.Path.GetFileNameWithoutExtension(exe).ToLowerInvariant();

            var titleSb = new StringBuilder(512);
            Native.GetWindowTextW(hwnd, titleSb, 512);

            lock (Gate)
            {
                var changed = name != _exe;
                _exe = name;
                _title = titleSb.ToString();
                if (changed) ForegroundChanged?.Invoke(_exe, _title);
            }
        }
        catch { /* never crash the poller */ }
    }
}

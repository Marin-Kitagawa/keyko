using System;
using System.Runtime.InteropServices;
using System.Threading;

// Validates Keyko's new thread-level hotkey engine:
//   1. RegisterHotKey(NULL, ...) — no window involved
//   2. Win+T → expected to fail (the shell owns it)
//   3. WM_HOTKEY delivery through the PeekMessage pump (same as the app's GetMessage loop)
// No keys are pressed or synthesized; registrations are unregistered at exit.
internal static class Program
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr h, int id);

    [DllImport("user32.dll")]
    private static extern bool PeekMessageW(out MSG m, IntPtr h, uint lo, uint hi, uint remove);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG m);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessageW(ref MSG m);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessageW(uint id, uint msg, IntPtr w, IntPtr l);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam, lParam;
        public uint time;
        public int ptX, ptY;
    }

    private const uint WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_WIN = 8, NOREPEAT = 0x4000;

    private static int Main()
    {
        var ok1 = RegisterHotKey(IntPtr.Zero, 1, MOD_CONTROL | MOD_ALT | NOREPEAT, (uint)'Y');
        Console.WriteLine($"[1] thread hotkey Ctrl+Alt+Y: {ok1} (lasterr={Marshal.GetLastWin32Error()})");

        var ok2 = RegisterHotKey(IntPtr.Zero, 2, MOD_WIN | NOREPEAT, (uint)'T');
        Console.WriteLine($"[2] thread hotkey Win+T:      {ok2} (lasterr={Marshal.GetLastWin32Error()})  <- shell owns Win+T, false expected");

        PostThreadMessageW(GetCurrentThreadId(), WM_HOTKEY, new IntPtr(1), new IntPtr(0));

        int hits = 0;
        var deadline = Environment.TickCount64 + 1500;
        while (Environment.TickCount64 < deadline)
        {
            while (PeekMessageW(out var m, IntPtr.Zero, 0, 0, 1))
            {
                TranslateMessage(ref m);
                if (m.message == WM_HOTKEY) { hits++; Console.WriteLine($"  WM_HOTKEY received: id={m.wParam}"); }
                DispatchMessageW(ref m);
            }
            Thread.Sleep(20);
        }
        Console.WriteLine($"WM_HOTKEY delivered through pump: {hits} (expect 1)");

        if (ok1) UnregisterHotKey(IntPtr.Zero, 1);
        if (ok2) UnregisterHotKey(IntPtr.Zero, 2);
        Console.WriteLine("probe done");
        return 0;
    }
}

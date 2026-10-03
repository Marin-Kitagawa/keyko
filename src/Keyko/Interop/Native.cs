using System;
using System.Text;
using System.Runtime.InteropServices;

namespace Keyko.Interop;

internal static class Native
{
    // ---- hotkey host window ----
    public delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern ushort RegisterClassW(ref WNDCLASSEXW lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateWindowExW(uint dwExStyle, string lpClassName, string? lpWindowName,
        uint dwStyle, int x, int y, int w, int h, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool GetMessageW(out MSG lpMsg, IntPtr hWnd, uint min, uint max);

    [DllImport("user32.dll")]
    public static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    public static extern IntPtr DispatchMessageW(ref MSG lpMsg);

    [DllImport("user32.dll")]
    public static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetWindowTextW(IntPtr hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetClipboardSequenceNumber();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PostThreadMessageW(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    public const uint WM_APP_APPLY = 0x8001; // WM_APP range: apply the hotkey set on the hotkey thread

    [StructLayout(LayoutKind.Sequential)]
    internal struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    public const uint WS_OVERLAPPED = 0;
    public static readonly IntPtr HWND_MESSAGE = new(-3);

    // ---- hotkeys ----
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const uint WM_HOTKEY = 0x0312;
    public const uint WM_QUIT = 0x0012;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandleW(string? lpModuleName);

    // ---- power & session ----
    [DllImport("user32.dll")]
    public static extern bool LockWorkStation();

    [DllImport("powrprof.dll")]
    public static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    // ---- window management ----
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool GetMonitorInfoW(IntPtr hMonitor, ref MONITORINFO info);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr newValue);

    public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));

    public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value) =>
        IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, value) : new IntPtr(SetWindowLong32(hWnd, nIndex, (int)value));

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int value);

    [DllImport("user32.dll")]
    public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint key, byte alpha, uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool CloseHandle(IntPtr handle);

    [DllImport("psapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool GetModuleFileNameEx(IntPtr process, IntPtr module, StringBuilder name, int size);

    public const int GWL_EXSTYLE = -20;
    public const uint WS_EX_LAYERED = 0x80000;
    public const uint LWA_ALPHA = 0x2;
    public const int GWL_STYLE = -16;
    public const uint PROCESS_QUERY_INFORMATION = 0x0400;
    public const uint PROCESS_VM_READ = 0x0010;
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public const int HWND_TOPMOST_GA = -1;      // used with SetWindowPos insert-after
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new(-2);
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    // ---- clipboard ----
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll")]
    public static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    public static extern bool CloseClipboard();

    // ---- low-level keyboard hook (text expansion) ----
    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookExW(int idHook, LowLevelKeyboardProc proc, IntPtr mod, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);

    public const int WH_KEYBOARD_LL = 13;
    public const uint WM_KEYDOWN_ = 0x0100;
    public const uint WM_SYSKEYDOWN_ = 0x0104;

    // ---- input simulation (snippet paste, win combos) ----
    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern short VkKeyScanW(char ch);

    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_UNICODE = 0x0004;
    public const ushort VK_CONTROL = 0x11;
    public const ushort VK_MENU = 0x12;
    public const ushort VK_SHIFT = 0x10;
    public const ushort VK_V = 0x56;
    public const ushort VK_LWIN = 0x5B;
    public const ushort VK_RWIN = 0x5C;
    public const ushort VK_VOLUME_MUTE = 0xAD;
    public const ushort VK_VOLUME_DOWN = 0xAE;
    public const ushort VK_VOLUME_UP = 0xAF;
    public const ushort VK_MEDIA_NEXT_TRACK = 0xB0;
    public const ushort VK_MEDIA_PREV_TRACK = 0xB1;
    public const ushort VK_MEDIA_PLAY_PAUSE = 0xB3;

    // ---- misc ----
    [DllImport("user32.dll")]
    public static extern IntPtr SendMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    public const uint WM_SYSCOMMAND = 0x0112;
    public static readonly IntPtr HWND_BROADCAST = new(0xFFFF);
    public static readonly IntPtr SC_MONITORPOWER = new(0xF170);

    // ---- window corners for borderless dialogs ----
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public static void RoundCorners(IntPtr hwnd)
    {
        try
        {
            var pref = 2; // DWMWCP_ROUND
            DwmSetWindowAttribute(hwnd, 33, ref pref, sizeof(int));
        }
        catch { /* pre-Win11: silently skip */ }
    }
}

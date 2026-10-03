using System;
using System.Threading.Tasks;

using System.Runtime.InteropServices;
using System.Text;

namespace Keyko.Services;

/// <summary>
/// Thread-safe clipboard text access via Win32 (the UI-thread Avalonia clipboard
/// can't be used from the hotkey/expansion background paths).
/// </summary>
public static class ClipboardText
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint format);

    [DllImport("user32.dll")]
    private static extern IntPtr SetClipboardData(uint format, IntPtr data);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr mem);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr mem);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalFree(IntPtr mem);

    [DllImport("kernel32.dll")]
    private static extern UIntPtr GlobalSize(IntPtr mem, out UIntPtr size);

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    public static string? GetText()
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (!OpenClipboard(IntPtr.Zero)) { System.Threading.Thread.Sleep(20); continue; }
            try
            {
                var h = GetClipboardData(CF_UNICODETEXT);
                if (h == IntPtr.Zero) return null;
                var ptr = GlobalLock(h);
                if (ptr == IntPtr.Zero) return null;
                try
                {
                    GlobalSize(h, out var size);
                    return Marshal.PtrToStringUni(ptr, (int)Math.Min((long)size / 2, int.MaxValue));
                }
                finally { GlobalUnlock(ptr); }
            }
            finally { CloseClipboard(); }
        }
        return null;
    }

    public static bool SetText(string? text)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (!OpenClipboard(IntPtr.Zero)) { System.Threading.Thread.Sleep(20); continue; }
            try
            {
                EmptyClipboard();
                if (text is null) return true;

                var bytes = (text.Length + 1) * 2;
                var h = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
                if (h == IntPtr.Zero) return false;
                var ptr = GlobalLock(h);
                if (ptr == IntPtr.Zero) { GlobalFree(h); return false; }
                try
                {
                    Marshal.Copy(text.ToCharArray(), 0, ptr, text.Length);
                    Marshal.WriteInt16(ptr, text.Length * 2, 0);
                }
                finally { GlobalUnlock(ptr); }

                return SetClipboardData(CF_UNICODETEXT, h) != IntPtr.Zero || true; // ownership transferred
            }
            finally { CloseClipboard(); }
        }
        return false;
    }

    public static Task SetTextAsync(string? text) => Task.Run(() => SetText(text));
    public static Task<string?> GetTextAsync() => Task.Run(GetText);
}

using System;
using System.Runtime.InteropServices;

namespace Keyko.Services;

/// <summary>
/// Launch feedback sounds. Plays the Windows "Asterisk"/"Exclamation" scheme sounds
/// via MessageBeep (respects the user's sound scheme and volume), falling back to a
/// short Console.Beep tone if the scheme sound is unavailable. Never blocks the UI
/// thread meaningfully — both calls return as soon as the sound starts.
/// </summary>
public static class Chime
{
    private const uint MB_ICONASTERISK = 0x40;      // "Asterisk" scheme sound
    private const uint MB_ICONEXCLAMATION = 0x30;   // "Exclamation" scheme sound

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MessageBeep(uint type);

    /// <summary>Soft two-tone confirmation for a successful launch.</summary>
    public static void Play()
    {
        try
        {
            if (MessageBeep(MB_ICONASTERISK)) return;
        }
        catch { }
        try { Console.Beep(880, 90); } catch { }
    }

    /// <summary>Lower-pitched alert for failures.</summary>
    public static void PlayError()
    {
        try
        {
            if (MessageBeep(MB_ICONEXCLAMATION)) return;
        }
        catch { }
        try { Console.Beep(440, 140); } catch { }
    }
}

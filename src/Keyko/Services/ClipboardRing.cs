using System;
using System.Collections.Generic;
using Keyko.Interop;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Keyko.Services;

/// <summary>
/// Ring buffer of the last N text clipboard entries, captured via a lightweight
/// poller on GetClipboardSequenceNumber. Items paste back with Ctrl+Win+V style
/// flows or from the Insights page.
/// </summary>
public static class ClipboardRing
{
    private static readonly object Gate = new();
    private static readonly List<string> Ring = new();
    private static System.Threading.Timer? _timer;
    private static uint _lastSeq;
    private const int Capacity = 12;

    /// <summary>Changed on the poller thread when a new text entry lands.</summary>
    public static event Action<string>? Added;

    public static void Start()
    {
        if (_timer is not null) return;
        _lastSeq = Native.GetClipboardSequenceNumber();
        _timer = new System.Threading.Timer(_ => Poll(), null, 1000, 800);
    }

    private static void Poll()
    {
        try
        {
            var seq = Native.GetClipboardSequenceNumber();
            if (seq == _lastSeq) return;
            _lastSeq = seq;

            var text = ClipboardText.GetText();
            if (string.IsNullOrWhiteSpace(text)) return;

            bool added = false;
            lock (Gate)
            {
                if (Ring.Count == 0 || Ring[0] != text)
                {
                    Ring.Remove(text);
                    Ring.Insert(0, text);
                    if (Ring.Count > Capacity) Ring.RemoveAt(Ring.Count - 1);
                    added = true;
                }
            }
            if (added) Added?.Invoke(text);
        }
        catch { /* clipboard may be locked by another process — skip tick */ }
    }

    public static IReadOnlyList<string> Items
    {
        get { lock (Gate) return Ring.ToArray(); }
    }

    public static void Clear()
    {
        lock (Gate) Ring.Clear();
    }
}

using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Keyko.Models;

namespace Keyko.Services;

/// <summary>
/// Fires scheduled shortcuts: Interval (every N minutes) or Daily (at HH:mm).
/// Runs on a background timer; actions are dispatched through a callback the
/// app wires to MainViewModel.ExecuteAsync.
/// </summary>
public static class SchedulerService
{
    private static System.Threading.Timer? _timer;
    private static Func<ShortcutAction, Task>? _execute;
    private static List<ShortcutAction> _scheduled = new();
    private static readonly object Gate = new();

    public static void Start(Func<ShortcutAction, Task> execute)
    {
        _execute = execute;
        _timer = new System.Threading.Timer(_ => Tick(), null, 5000, 20_000);
    }

    public static void SetScheduled(IEnumerable<ShortcutAction> actions)
    {
        lock (Gate)
            _scheduled = actions
                .Where(a => a.Enabled && a.Schedule != ScheduleMode.None)
                .Select(a => a.Clone())
                .ToList();
    }

    private static void Tick()
    {
        if (KeykoState.Paused) return;
        ShortcutAction[] due;
        lock (Gate)
        {
            due = _scheduled.Where(IsDue).ToArray();
        }
        var exec = _execute;
        if (exec is null) return;
        foreach (var a in due)
        {
            a.LastFiredAt = DateTime.Now;
            _ = exec(a);
        }
    }

    private static bool IsDue(ShortcutAction a)
    {
        var now = DateTime.Now;
        return a.Schedule switch
        {
            ScheduleMode.Interval => a.LastFiredAt is null
                || (now - a.LastFiredAt.Value).TotalMinutes >= Math.Max(1, a.ScheduleIntervalMinutes),
            ScheduleMode.Daily => a.ScheduleDailyTime is { } t
                && TimeSpan.TryParse(t, out var time)
                && now.TimeOfDay >= time
                && (a.LastFiredAt is null || a.LastFiredAt.Value.Date < now.Date),
            _ => false,
        };
    }
}

using System;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Threading;
using Keyko.Services;

namespace Keyko;

internal static class Program
{
    private static EventWaitHandle? _activateEvent;
    private static Mutex? _singleInstanceMutex;

    [STAThread]
    public static int Main(string[] args)
    {
        var selftest = args.Contains("--selftest");
        if (selftest)
        {
            // never touch the user's real profile while testing
            Environment.SetEnvironmentVariable("KEYKO_CONFIG_DIR",
                Path.Combine(Path.GetTempPath(), "Keyko.Selftest"));
        }
        else if (OperatingSystem.IsWindows())
        {
            // named mutex/event handles are Windows-only; other platforms allow parallel instances
            _singleInstanceMutex = new Mutex(true, @"Local\Keyko.SingleInstance", out var createdNew);
            if (!createdNew)
            {
                // another instance is shutting down (e.g. admin relaunch) — wait for the handover
                for (int i = 0; i < 16 && !createdNew; i++)
                {
                    try { createdNew = _singleInstanceMutex.WaitOne(TimeSpan.FromMilliseconds(250)); }
                    catch (AbandonedMutexException) { createdNew = true; }
                }
            }
            if (!createdNew)
            {
                try
                {
                    using var existing = EventWaitHandle.OpenExisting(@"Local\Keyko.Activate");
                    existing.Set();
                }
                catch { /* other instance may still be starting */ }
                return 0;
            }

            _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Keyko.Activate");
            var watcher = new Thread(() =>
            {
                while (_activateEvent.WaitOne())
                    Dispatcher.UIThread.Post(() => AppServices.MainWindow?.BringToFrontFromTray());
            })
            { IsBackground = true, Name = "Keyko.ActivateWatcher" };
            watcher.Start();
        }

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            _singleInstanceMutex?.ReleaseMutex();
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace Keyko.Services;

public static class AutostartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Keyko";
    private const string TaskName = "Keyko";

    /// <summary>True when autostart is configured through either mechanism.</summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            if (key?.GetValue(ValueName) is string) return true;
        }
        catch { }
        return TaskExists();
    }

    public static bool TaskExists()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("schtasks", $"/Query /TN {TaskName}")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            });
            p?.WaitForExit(10_000);
            return p?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled) => Apply(enabled, runAsAdmin: false);

    /// <summary>
    /// Normal autostart uses the registry Run key. Admin autostart uses a scheduled task
    /// with highest privileges, which starts elevated at logon without a UAC prompt.
    /// </summary>
    public static void Apply(bool launchOnStartup, bool runAsAdmin)
    {
        if (!OperatingSystem.IsWindows())
            return; // registry / schtasks are Windows-only

        DeleteRunValue();
        DeleteTask();

        if (!launchOnStartup) return;

        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return;

        if (runAsAdmin)
        {
            // schtasks receives: /TR "\"C:\...\Keyko.exe\" --minimized"
            var tr = $"\\\"{exe}\\\" --minimized";
            var args = $"/Create /F /TN {TaskName} /SC ONLOGON /RL HIGHEST /TR \"{tr}\"";
            try
            {
                using var p = Process.Start(new ProcessStartInfo("schtasks", args)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                });
                p?.WaitForExit(15_000);
                if (p?.ExitCode == 0) return;
            }
            catch { }

            // couldn't create the task — fall back to the registry (starts unelevated)
            SetRunValue(exe);
        }
        else
        {
            SetRunValue(exe);
        }
    }

    private static void SetRunValue(string exe)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            key.SetValue(ValueName, $"\"{exe}\" --minimized");
        }
        catch { }
    }

    private static void DeleteRunValue()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch { }
    }

    private static void DeleteTask()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("schtasks", $"/Delete /TN {TaskName} /F")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            });
            p?.WaitForExit(10_000);
        }
        catch { }
    }
}

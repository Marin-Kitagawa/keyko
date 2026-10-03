using System;
using System.Diagnostics;
using System.Security.Principal;

namespace Keyko.Services;

public static class Elevation
{
    public static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Spawns a new instance of Keyko (optionally via the UAC prompt).
    /// Returns false if the user cancelled the prompt or launching failed.
    /// </summary>
    public static bool TryRelaunch(bool asAdmin)
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return false;

            var minimized = AppServices.MainWindow is { } w && !w.IsVisible;
            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = true,
                Arguments = minimized ? "--minimized" : "",
            };
            if (asAdmin) psi.Verb = "runas";

            Process.Start(psi);
            return true;
        }
        catch
        {
            // Win32Exception when the UAC prompt is cancelled
            return false;
        }
    }
}

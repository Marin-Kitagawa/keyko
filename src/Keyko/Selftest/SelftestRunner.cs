using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Keyko.Models;
using Keyko.Services;
using Keyko.ViewModels;
using Keyko.Views;

namespace Keyko.Selftest;

/// <summary>
/// Headless-ish visual check: renders the UI and saves PNGs without ever
/// activating a window or synthesizing input (safe while the user is busy).
/// Usage: Keyko --selftest main.png editor.png settings.png
///
/// NOTE: RunAsync runs on the UI thread (resumed there by the dispatcher sync
/// context), so window captures are plain synchronous calls â do NOT await
/// InvokeAsync here: that continuation can deadlock behind a modal dialog loop.
/// </summary>
public static class SelftestRunner
{
    public static void Begin(MainWindow main)
    {
        var args = App.CliArgs;
        var i = Array.IndexOf(args, "--selftest");
        var paths = args.Skip(i + 1).ToArray();
        _ = RunAsync(main,
            paths.Length > 0 ? paths[0] : "selftest-main.png",
            paths.Length > 1 ? paths[1] : "selftest-editor.png",
            paths.Length > 2 ? paths[2] : "selftest-settings.png");
    }

    private static async Task RunAsync(MainWindow main, string outMain, string outEditor, string outSettings)
    {
        try
        {
            var vm = main.DataContext as MainViewModel ?? App.Vm!;
            await Task.Delay(1000);
            Shot(main, outMain);

            // show the toast early so an external capture can grab it pre-dialog
            ToastService.ShowNow("â¡ 5 hotkeys armed", "They work system-wide — even from the tray.", "");

            vm.SetPage(NavPage.Settings);
            await Task.Delay(500);
            Shot(main, outSettings);

            vm.SetPage(NavPage.Shortcuts);
            await Task.Delay(300);

            var first = vm.Items.FirstOrDefault(x => x.Model.Type == ActionType.KeySequence)
                        ?? vm.Items.FirstOrDefault();
            if (first is not null)
            {
                var clone = JsonSerializer.Deserialize<ShortcutAction>(JsonSerializer.Serialize(first.Model))!;
                clone.Id = first.Model.Id;
                var dlg = new ShortcutEditorWindow(new ShortcutEditorViewModel(
                    clone, vm.Items.Where(x => x.Model.Id != clone.Id).Select(x => x.Model)))
                {
                    IsNew = false,
                    ShowActivated = false,
                };
                _ = dlg.ShowDialog(main);
                await Task.Delay(700);
                Shot(dlg, outEditor);
                if (!App.CliArgs.Contains("--keep-open"))
                    dlg.Close();
            }

            await Task.Delay(200);
            if (!App.CliArgs.Contains("--keep-open"))
                (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown(0);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown(1);
        }
    }

    private static void Shot(Window w, string path)
    {
        var scale = w.RenderScaling;
        var size = new PixelSize(
            (int)Math.Ceiling(w.Bounds.Width * scale),
            (int)Math.Ceiling(w.Bounds.Height * scale));
        var rtb = new RenderTargetBitmap(size, new Vector(96 * scale, 96 * scale));
        rtb.Render(w);
        using var fs = File.Create(path);
        rtb.Save(fs);
        Console.WriteLine("Keyko selftest: saved " + Path.GetFullPath(path));
    }
}

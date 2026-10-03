using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Keyko.Interop;
using Keyko.Models;
using Keyko.Services;
using Keyko.ViewModels;

namespace Keyko.Views;

public partial class ShortcutEditorWindow : Window
{
    public bool Saved { get; private set; }
    public bool IsNew { get; set; }

    // parameterless ctor required by the XAML compiler; real construction uses the other one
    public ShortcutEditorWindow()
    {
        InitializeComponent();
        Opened += OnFirstOpened;
        Closed += (_, _) => GlassService.Changed -= OnGlassChanged;
        CloseBtn.Click += (_, _) => Close();
        CancelBtn.Click += (_, _) => Close();
        SaveBtn.Click += OnSave;
        TestRunBtn.Click += OnTestRun;
        DragZone.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                try { BeginMoveDrag(e); } catch { }
            }
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && !Recorder.IsFocused)
            {
                e.Handled = true;
                Close();
            }
        };
        Recorder.KeyDown += OnRecorderKeyDown;
        Recorder.PointerPressed += (_, _) => Recorder.Focus();
    }

    public ShortcutEditorWindow(ShortcutEditorViewModel vm) : this()
    {
        DataContext = vm;
    }

    private ShortcutEditorViewModel Vm => (ShortcutEditorViewModel)DataContext!;

    public ShortcutEditorViewModel ViewModel => Vm;

    private void OnFirstOpened(object? sender, EventArgs e)
    {
        GlassService.ApplyTo(Acrylic.Material!, AppServices.Config.Settings);
        GlassService.Changed += OnGlassChanged;
        if (App.IsSelftest) BaseWallpaper.IsVisible = true;

        if (TryGetPlatformHandle()?.Handle is { } hwnd)
            Native.RoundCorners(hwnd);

        TitleText.Text = IsNew ? "New shortcut" : "Edit shortcut";

        if (this.FindControl<TextBox>("NameBox") is { } nameBox && string.IsNullOrEmpty(Vm.Name))
            nameBox.Focus();
        else
            Recorder.Focus();
    }

    private void OnGlassChanged()
    {
        try { GlassService.ApplyTo(Acrylic.Material!, AppServices.Config.Settings); }
        catch { /* closing */ }
    }

    // ---------- save / test ----------
    private void OnSave(object? sender, RoutedEventArgs e)
    {
        Vm.BuildModel(Vm.Model);
        Saved = true;
        Close();
    }

    private async void OnTestRun(object? sender, RoutedEventArgs e)
    {
        var probe = new ShortcutAction { Id = "probe-" + Guid.NewGuid().ToString("N"), ShowToast = false };
        Vm.BuildModel(probe);
        var (ok, error) = await ActionRunner.RunAsync(probe, this);
        if (ok)
            ToastService.Show("Test run OK", probe.Name, "✅", settings: AppServices.Config.Settings);
        else
            ToastService.Show("Test run failed", error, "⚠️", settings: null);
    }

    // ---------- type selector ----------
    private void OnTypeClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } &&
            Enum.TryParse<ActionType>(tag, out var type))
        {
            Vm.SetType(type);
        }
    }

    // ---------- browse ----------
    private async void OnBrowseFile(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a program or shortcut",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Programs") { Patterns = new[] { "*.exe", "*.lnk", "*.url", "*.bat", "*.cmd", "*.ps1" } },
                FilePickerFileTypes.All,
            },
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            Vm.TargetText = path;
            if (string.IsNullOrWhiteSpace(Vm.Name))
                Vm.Name = System.IO.Path.GetFileNameWithoutExtension(path);
        }
    }

    private async void OnBrowseFolder(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose a folder",
            AllowMultiple = false,
        });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            Vm.TargetText = path;
            if (string.IsNullOrWhiteSpace(Vm.Name))
                Vm.Name = System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar));
        }
    }

    // ---------- hotkey recorder ----------
    private void OnRecorderKeyDown(object? sender, KeyEventArgs e)
    {
        var key = e.Key;
        e.Handled = true;

        if (key is Key.None or Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
            return;

        if (key == Key.Escape)
        {
            Vm.ClearHotkey();
            return;
        }

        Vm.SetHotkey(new HotkeyGesture(key, e.KeyModifiers));
    }

    private void OnClearHotkey(object? sender, RoutedEventArgs e) => Vm.ClearHotkey();

    // ---------- sequence tokens ----------
    private void OnInsertSeqToken(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Content: string token })
        {
            Vm.SequenceText = (Vm.SequenceText.Length > 0 && !Vm.SequenceText.EndsWith(' ')
                ? Vm.SequenceText + " "
                : Vm.SequenceText) + token;
        }
    }
}

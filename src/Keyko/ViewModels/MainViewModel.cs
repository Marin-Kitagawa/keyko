using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Keyko.Models;
using Keyko.Services;
using Keyko.Views;

namespace Keyko.ViewModels;

public enum NavPage { Shortcuts, Settings, About }

public partial class MainViewModel : ViewModelBase
{
    private readonly ConfigService _config;

    private HotkeyService Hotkeys => AppServices.Hotkeys;

    public MainViewModel(ConfigService config)
    {
        _config = config;

        foreach (var m in config.Settings.Shortcuts)
            AddItem(new ShortcutItemViewModel(m, config.IconsDir));

        Hotkeys.HotkeyPressed += OnHotkeyPressed;
        Hotkeys.Conflicts += OnConflicts;

        RebuildCategories();
        ApplyFilter();
        UpdateStats();
    }

    public ConfigService Config => _config;

    public SettingsViewModel SettingsVM => new(_config, this);

    public ObservableCollection<ShortcutItemViewModel> Items { get; } = new();

    public ObservableCollection<ShortcutItemViewModel> FilteredItems { get; } = new();

    public ObservableCollection<CategoryChipViewModel> Categories { get; } = new();

    // ---- page nav ----
    public event Action<NavPage>? PageChanged;

    [ObservableProperty]
    private NavPage _currentPage = NavPage.Shortcuts;

    public bool IsOnShortcuts => CurrentPage == NavPage.Shortcuts;
    public bool IsOnSettings => CurrentPage == NavPage.Settings;
    public bool IsOnAbout => CurrentPage == NavPage.About;
    public bool HasVisibleItems => FilteredItems.Count > 0;

    partial void OnCurrentPageChanged(NavPage value)
    {
        OnPropertyChanged(nameof(IsOnShortcuts));
        OnPropertyChanged(nameof(IsOnSettings));
        OnPropertyChanged(nameof(IsOnAbout));
    }

    [RelayCommand]
    private void ShowShortcuts() => SetPage(NavPage.Shortcuts);

    [RelayCommand]
    private void ShowSettings() => SetPage(NavPage.Settings);

    [RelayCommand]
    private void ShowAbout() => SetPage(NavPage.About);

    public void SetPage(NavPage p)
    {
        if (CurrentPage == p) return;
        CurrentPage = p;
        PageChanged?.Invoke(p);
    }

    // ---- search / filter ----
    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private CategoryChipViewModel? _selectedCategory;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedCategoryChanged(CategoryChipViewModel? value) => ApplyFilter();

    // ---- stats ----
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _activeCount;
    [ObservableProperty] private int _totalRuns;
    [ObservableProperty] private bool _hasConflicts;
    [ObservableProperty] private string _conflictText = "";

    private void UpdateStats()
    {
        TotalCount = Items.Count;
        ActiveCount = Items.Count(i => i.IsEnabled && i.HasHotkey);
        TotalRuns = Items.Sum(i => i.Model.RunCount);
    }

    private void ApplyFilter()
    {
        var q = SearchText.Trim();
        FilteredItems.Clear();
        foreach (var item in Items)
        {
            if (SelectedCategory is { } cat && !cat.IsAll && !string.Equals(item.Category, cat.Name, StringComparison.OrdinalIgnoreCase))
                continue;
            if (q.Length > 0 &&
                !(item.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                  || item.Description.Contains(q, StringComparison.OrdinalIgnoreCase)
                  || item.Category.Contains(q, StringComparison.OrdinalIgnoreCase)
                  || item.HotkeyDisplay.Contains(q, StringComparison.OrdinalIgnoreCase)
                  || item.Model.Target.Contains(q, StringComparison.OrdinalIgnoreCase)))
                continue;
            FilteredItems.Add(item);
        }
        OnPropertyChanged(nameof(HasVisibleItems));
    }

    private void RebuildCategories()
    {
        var selected = SelectedCategory?.Name;
        Categories.Clear();
        Categories.Add(new CategoryChipViewModel("All", Items.Count, selected is null || selected == "All"));
        foreach (var g in Items.GroupBy(i => string.IsNullOrWhiteSpace(i.Category) ? "General" : i.Category)
                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            Categories.Add(new CategoryChipViewModel(g.Key, g.Count(), selected == g.Key));
    }

    [RelayCommand]
    private void SelectCategory(CategoryChipViewModel? chip)
    {
        if (chip is null) return;
        SelectedCategory = chip;
        foreach (var c in Categories) c.IsSelected = c == chip;
    }

    // ---- items ----
    private void AddItem(ShortcutItemViewModel item)
    {
        item.Toggled += OnItemToggled;
        Items.Add(item);
    }

    private void OnItemToggled(ShortcutItemViewModel item)
    {
        _config.Save();
        Hotkeys.Apply(_config.Settings.Shortcuts);
        UpdateStats();
    }

    private void OnHotkeyPressed(ShortcutAction action)
    {
        _ = ExecuteAsync(action, manual: false);
    }

    private void OnConflicts(System.Collections.Generic.IReadOnlyList<ShortcutAction> failed)
    {
        var names = string.Join(", ", failed.Select(f => f.Gesture?.Display ?? f.Name));
        ConflictText = names.Length == 0 ? "" : $"Hotkey(s) {names} couldn't be registered — already used by another app.";
        HasConflicts = ConflictText.Length > 0;
    }

    public void ClearConflictBanner()
    {
        HasConflicts = false;
        ConflictText = "";
    }

    [RelayCommand]
    private void ClearConflict() => ClearConflictBanner();

    public async Task ExecuteAsync(ShortcutAction action, bool manual)
    {
        var item = Items.FirstOrDefault(i => i.Model.Id == action.Id);
        var host = AppServices.MainWindow;
        if (host is null) return;

        var (ok, error) = await ActionRunner.RunAsync(action, host);
        if (ok)
        {
            action.RunCount++;
            action.LastUsedAt = DateTime.Now;
            item?.Refresh(false);
            UpdateStats();
            _config.Save();
            ToastService.Show(action.Name,
                manual ? "Launched manually" : action.Gesture?.Display,
                emoji: action.TileGlyph,
                settings: _config.Settings);
        }
        else
        {
            ToastService.Show("Couldn't launch " + action.Name, error, emoji: "⚠️", settings: null);
        }
    }

    [RelayCommand]
    private Task RunNow(ShortcutItemViewModel? item) => item is null ? Task.CompletedTask : ExecuteAsync(item.Model, manual: true);

    // ---- editor flow ----
    [RelayCommand]
    public async Task NewShortcutAsync()
    {
        var model = new ShortcutAction
        {
            Category = SelectedCategory is { IsAll: false } ? SelectedCategory.Name : "General",
        };
        await OpenEditorAsync(model, isNew: true);
    }

    [RelayCommand]
    public async Task EditAsync(ShortcutItemViewModel? item)
    {
        if (item is null) return;

        // clone into a scratch model so Cancel is lossless
        var scratch = JsonSerializer.Deserialize<ShortcutAction>(JsonSerializer.Serialize(item.Model))!;
        scratch.Id = item.Model.Id;
        await OpenEditorAsync(scratch, isNew: false, original: item.Model, item: item);
    }

    private async Task OpenEditorAsync(ShortcutAction model, bool isNew,
        ShortcutAction? original = null, ShortcutItemViewModel? item = null)
    {
        var dlg = new ShortcutEditorWindow(new ShortcutEditorViewModel(
            model, _config.Settings.Shortcuts.Where(x => x.Id != model.Id)));
        dlg.IsNew = isNew;
        await dlg.ShowDialog(AppServices.MainWindow!);
        if (!dlg.Saved) return;

        var oldTarget = original?.Target;
        var oldType = original?.Type;

        if (isNew)
        {
            _config.Settings.Shortcuts.Add(model);
            AddItem(new ShortcutItemViewModel(model, _config.IconsDir));
        }
        else if (original is not null && item is not null)
        {
            dlg.ViewModel.BuildModel(original);
            if (oldType == Models.ActionType.Application && oldTarget != original.Target)
                IconCacheService.Invalidate(original.Id, _config.IconsDir);
            item.Refresh(true);
        }

        _config.Save();
        Hotkeys.Apply(_config.Settings.Shortcuts);
        RebuildCategories();
        ApplyFilter();
        UpdateStats();
    }

    [RelayCommand]
    private void Delete(ShortcutItemViewModel? item)
    {
        if (item is null) return;
        var index = Items.IndexOf(item);
        Items.Remove(item);
        _config.Settings.Shortcuts.Remove(item.Model);
        Hotkeys.Apply(_config.Settings.Shortcuts);
        RebuildCategories();
        ApplyFilter();
        UpdateStats();
        _config.Save();

        ToastService.Show("Shortcut deleted", item.Name, emoji: "🗑️",
            settings: _config.Settings,
            action: ("Undo", () =>
            {
                _config.Settings.Shortcuts.Insert(Math.Min(index, _config.Settings.Shortcuts.Count), item.Model);
                var vm = new ShortcutItemViewModel(item.Model, _config.IconsDir);
                Items.Insert(Math.Min(index, Items.Count), vm);
                Hotkeys.Apply(_config.Settings.Shortcuts);
                RebuildCategories();
                ApplyFilter();
                UpdateStats();
                _config.Save();
            }));
    }

    [RelayCommand]
    private async Task DuplicateAsync(ShortcutItemViewModel? item)
    {
        if (item is null) return;
        var clone = JsonSerializer.Deserialize<ShortcutAction>(JsonSerializer.Serialize(item.Model))!;
        clone.Id = Guid.NewGuid().ToString("N");
        clone.Name = item.Model.Name + " (copy)";
        clone.Hotkey = null; // avoid instant conflict
        clone.RunCount = 0;
        clone.LastUsedAt = null;
        _config.Settings.Shortcuts.Add(clone);
        AddItem(new ShortcutItemViewModel(clone, _config.IconsDir));
        RebuildCategories();
        ApplyFilter();
        UpdateStats();
        _config.Save();
        await Task.CompletedTask;
    }

    // ---- import / export ----
    private static readonly FilePickerFileType JsonType = new("JSON profile")
    {
        Patterns = new[] { "*.json" },
        MimeTypes = new[] { "application/json" },
    };

    [RelayCommand]
    public async Task ImportAsync()
    {
        var win = AppServices.MainWindow;
        if (win?.StorageProvider is null) return;
        var files = await win.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Keyko profile",
            AllowMultiple = false,
            FileTypeFilter = new[] { JsonType },
        });
        if (files.Count == 0) return;

        try
        {
            await using var fs = await files[0].OpenReadAsync();
            using var reader = new StreamReader(fs);
            var json = await reader.ReadToEndAsync();
            using var doc = JsonDocument.Parse(json);
            JsonElement arr;
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                arr = doc.RootElement.Clone();
            else if (doc.RootElement.TryGetProperty("Shortcuts", out var s) && s.ValueKind == JsonValueKind.Array)
                arr = s.Clone();
            else throw new InvalidDataException("No shortcuts found in file.");

            var imported = JsonSerializer.Deserialize<ShortcutAction[]>(arr.GetRawText()) ?? Array.Empty<ShortcutAction>();
            int n = 0;
            foreach (var m in imported)
            {
                m.Id = Guid.NewGuid().ToString("N");
                _config.Settings.Shortcuts.Add(m);
                AddItem(new ShortcutItemViewModel(m, _config.IconsDir));
                n++;
            }
            _config.Save();
            Hotkeys.Apply(_config.Settings.Shortcuts);
            RebuildCategories();
            ApplyFilter();
            UpdateStats();
            ToastService.Show($"Imported {n} shortcut" + (n == 1 ? "" : "s"), null, "📥", settings: null);
        }
        catch (Exception ex)
        {
            ToastService.Show("Import failed", ex.Message, "⚠️", settings: null);
        }
    }

    [RelayCommand]
    public async Task ExportAsync()
    {
        var win = AppServices.MainWindow;
        if (win?.StorageProvider is null) return;
        var file = await win.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Keyko profile",
            SuggestedFileName = "keyforge-profile",
            DefaultExtension = "json",
            FileTypeChoices = new[] { JsonType },
        });
        if (file is null) return;

        try
        {
            await using var fs = await file.OpenWriteAsync();
            await JsonSerializer.SerializeAsync(fs, _config.Settings.Shortcuts,
                new JsonSerializerOptions { WriteIndented = true });
            ToastService.Show("Profile exported", file.Name, "📤", settings: null);
        }
        catch (Exception ex)
        {
            ToastService.Show("Export failed", ex.Message, "⚠️", settings: null);
        }
    }

    public void RegisterHotkeys() => Hotkeys.Apply(_config.Settings.Shortcuts);
}

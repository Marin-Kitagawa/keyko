using CommunityToolkit.Mvvm.ComponentModel;

namespace Keyko.ViewModels;

public partial class CategoryChipViewModel : ObservableObject
{
    public string Name { get; }
    public int Count { get; }

    [ObservableProperty]
    private bool _isSelected;

    public bool IsAll => Name == "All";

    public CategoryChipViewModel(string name, int count, bool isSelected = false)
    {
        Name = name;
        Count = count;
        _isSelected = isSelected;
    }

    public string Display => IsAll ? $"All · {Count}" : $"{Name} · {Count}";
}

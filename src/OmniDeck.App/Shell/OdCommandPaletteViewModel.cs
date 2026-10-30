using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmniDeck.Core.Search;

namespace OmniDeck.App.Shell;

/// <summary>The command palette (Ctrl+K, docs/SPEC.md §2): fuzzy search over modules and shell commands.</summary>
public sealed partial class OdCommandPaletteViewModel : ObservableObject
{
    private readonly IReadOnlyList<OdPaletteEntry> _entries;

    [ObservableProperty]
    private string _query = "";

    [ObservableProperty]
    private OdPaletteEntry? _selected;

    public OdCommandPaletteViewModel(IReadOnlyList<OdPaletteEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = entries;
        Filter();
    }

    /// <summary>Raised when the user accepts <see cref="Chosen"/>.</summary>
    public event EventHandler? Accepted;

    public ObservableCollection<OdPaletteEntry> Results { get; } = [];

    /// <summary>The accepted entry; <see langword="null"/> while the palette is open or after it was dismissed.</summary>
    public OdPaletteEntry? Chosen { get; private set; }

    /// <summary>Moves the selection by <paramref name="delta"/> rows, stopping at either end.</summary>
    public void MoveSelection(int delta)
    {
        if (Results.Count == 0)
        {
            return;
        }

        var current = Selected is null ? -1 : Results.IndexOf(Selected);
        Selected = Results[Math.Clamp(current + delta, 0, Results.Count - 1)];
    }

    partial void OnQueryChanged(string value) => Filter();

    [RelayCommand]
    private void Accept()
    {
        if (Selected is null)
        {
            return;
        }

        Chosen = Selected;
        Accepted?.Invoke(this, EventArgs.Empty);
    }

    private void Filter()
    {
        Results.Clear();
        foreach (var entry in OdFuzzyMatcher.Rank(Query, _entries, entry => entry.Title))
        {
            Results.Add(entry);
        }

        Selected = Results.Count > 0 ? Results[0] : null;
    }
}

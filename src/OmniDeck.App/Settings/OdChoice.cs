using CommunityToolkit.Mvvm.ComponentModel;

namespace OmniDeck.App.Settings;

/// <summary>
/// One entry of a settings choice: the value stored and the text shown. The text follows the UI language
/// in place (<see cref="RefreshTitle"/>): a list rebuilt instead would clear the selection bound to it.
/// </summary>
public sealed class OdChoice<T> : ObservableObject
{
    private readonly Func<string> _title;

    public OdChoice(T value, Func<string> title)
    {
        ArgumentNullException.ThrowIfNull(title);
        Value = value;
        _title = title;
        Title = title();
    }

    /// <summary>A choice whose text is not localized, such as a module's name.</summary>
    public OdChoice(T value, string title)
        : this(value, () => title)
    {
    }

    public T Value { get; }

    public string Title { get; private set; }

    /// <summary>Reads the text again, after a change of language.</summary>
    public void RefreshTitle()
    {
        Title = _title();
        OnPropertyChanged(nameof(Title));
    }

    /// <summary>The text shown: screen readers name a list item after its data.</summary>
    public override string ToString() => Title;
}

using System.ComponentModel;

namespace OmniDeck.Core.Localization;

/// <summary>
/// An entry of a list whose label follows the UI language in place (<see cref="RefreshLabel"/>): the
/// selection bound to the list stays when the language changes, which a rebuilt list would clear.
/// </summary>
public abstract class OdLocalizedOption(Func<string> label) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Label { get; private set; } = label();

    /// <summary>Reads the label again, after a change of language.</summary>
    public void RefreshLabel()
    {
        Label = label();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
    }

    /// <summary>The label: screen readers name a list item after its data.</summary>
    public override string ToString() => Label;
}

using CommunityToolkit.Mvvm.ComponentModel;
using OmniDeck.Core.Localization;

namespace OmniDeck.App.Settings;

/// <summary>An accent color to pick in Settings → Appearance, with the name screen readers say for it.</summary>
public sealed class OdAccentSwatch : ObservableObject
{
    private readonly Func<string> _name;

    public OdAccentSwatch(string color, Func<string> name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Color = color;
        _name = name;
        Name = name();
    }

    public string Color { get; }

    public string Name { get; private set; }

    /// <summary>The swatches, in the order Settings shows them.</summary>
    public static IReadOnlyList<OdAccentSwatch> All() =>
    [
        new("#0067C0", () => OdLocalization.Get("Accent_Blue")),
        new("#8764B8", () => OdLocalization.Get("Accent_Purple")),
        new("#C239B3", () => OdLocalization.Get("Accent_Pink")),
        new("#E74856", () => OdLocalization.Get("Accent_Red")),
        new("#CA5010", () => OdLocalization.Get("Accent_Orange")),
        new("#10893E", () => OdLocalization.Get("Accent_Green")),
    ];

    /// <summary>Reads the name again, after a change of language.</summary>
    public void RefreshName()
    {
        Name = _name();
        OnPropertyChanged(nameof(Name));
    }

    /// <summary>The color's name: screen readers name a list item after its data.</summary>
    public override string ToString() => Name;
}

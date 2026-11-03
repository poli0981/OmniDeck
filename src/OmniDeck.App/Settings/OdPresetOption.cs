using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using OmniDeck.Core.Localization;
using OmniDeck.Core.Theming;

namespace OmniDeck.App.Settings;

/// <summary>A UI preset as the preset cards show it (docs/UI-MODES.md); its texts follow the UI language in place.</summary>
public sealed class OdPresetOption : ObservableObject
{
    public OdPresetOption(string name, ResourceDictionary? preview = null)
    {
        Name = name;
        Preview = preview;
        Title = OdLocalization.Get("Preset_" + name);
        Hint = OdLocalization.Get("Preset_" + name + "Hint");
    }

    public string Name { get; }

    public string Title { get; private set; }

    public string Hint { get; private set; }

    /// <summary>The preset's own layer, when the card previews it (<see cref="Theming.OdPresetPreview"/>).</summary>
    public ResourceDictionary? Preview { get; }

    /// <summary>The three presets, in the order the cards show them, each with its preview when <paramref name="preview"/> builds one.</summary>
    public static IReadOnlyList<OdPresetOption> All(Func<OdPreset, ResourceDictionary>? preview = null) =>
        [.. OdPreset.All.Select(preset => new OdPresetOption(preset.Name, preview?.Invoke(preset)))];

    /// <summary>Reads the texts again, after a change of language.</summary>
    public void RefreshTexts()
    {
        Title = OdLocalization.Get("Preset_" + Name);
        Hint = OdLocalization.Get("Preset_" + Name + "Hint");
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Hint));
    }

    /// <summary>The card's title: screen readers name a list item after its data.</summary>
    public override string ToString() => Title;
}

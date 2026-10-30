using OmniDeck.Core.Localization;
using OmniDeck.Core.Theming;

namespace OmniDeck.App.Settings;

/// <summary>A UI preset as the preset cards show it (docs/UI-MODES.md).</summary>
public sealed record OdPresetOption(string Name, string Title, string Hint)
{
    /// <summary>The three presets, in the order the cards show them.</summary>
    public static IReadOnlyList<OdPresetOption> All() =>
        [.. OdPreset.All.Select(preset => new OdPresetOption(preset.Name, OdLocalization.Get("Preset_" + preset.Name), OdLocalization.Get("Preset_" + preset.Name + "Hint")))];

    /// <summary>The card's title: screen readers name a list item after its data.</summary>
    public override string ToString() => Title;
}

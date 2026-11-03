using System.Windows;
using OmniDeck.Core.Localization;
using OmniDeck.Core.Theming;

namespace OmniDeck.App.Settings;

/// <summary>A UI preset as the preset cards show it (docs/UI-MODES.md).</summary>
public sealed record OdPresetOption(string Name, string Title, string Hint)
{
    /// <summary>The preset's own layer, when the card previews it (<see cref="Theming.OdPresetPreview"/>).</summary>
    public ResourceDictionary? Preview { get; init; }

    /// <summary>The three presets, in the order the cards show them, each with its preview when <paramref name="preview"/> builds one.</summary>
    public static IReadOnlyList<OdPresetOption> All(Func<OdPreset, ResourceDictionary>? preview = null) =>
        [.. OdPreset.All.Select(preset => new OdPresetOption(preset.Name, OdLocalization.Get("Preset_" + preset.Name), OdLocalization.Get("Preset_" + preset.Name + "Hint"))
        {
            Preview = preview?.Invoke(preset),
        })];

    /// <summary>The card's title: screen readers name a list item after its data.</summary>
    public override string ToString() => Title;
}

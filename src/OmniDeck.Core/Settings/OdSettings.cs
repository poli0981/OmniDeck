namespace OmniDeck.Core.Settings;

/// <summary>
/// The typed settings tree persisted to <c>settings.json</c> in the data root. Immutable: changes go
/// through <see cref="IOdSettingsService.Update"/> with a <c>with</c> expression.
/// </summary>
public sealed record OdSettings
{
    /// <summary>The version of this file's shape, for future migrations.</summary>
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>UI culture name such as <c>en</c>; empty follows Windows.</summary>
    public string Language { get; init; } = "";

    public OdThemeMode Theme { get; init; } = OdThemeMode.System;

    /// <summary>The UI preset: <c>Dynamic</c>, <c>Standard</c> or <c>Comfort</c> (docs/UI-MODES.md).</summary>
    public string Preset { get; init; } = DefaultPreset;

    /// <summary>Closing the main window keeps OmniDeck running in the tray.</summary>
    public bool CloseToTray { get; init; } = true;

    public bool StartWithWindows { get; init; }

    internal const string DefaultPreset = "Standard";

    internal static readonly string[] KnownPresets = ["Dynamic", "Standard", "Comfort"];

    /// <summary>Repairs values a hand-edited or older file may carry.</summary>
    public OdSettings Normalized() => this with
    {
        SchemaVersion = CurrentSchemaVersion,
        Language = string.IsNullOrWhiteSpace(Language) ? "" : Language.Trim(),
        Theme = Enum.IsDefined(Theme) ? Theme : OdThemeMode.System,
        Preset = KnownPresets.FirstOrDefault(p => string.Equals(p, Preset, StringComparison.OrdinalIgnoreCase)) ?? DefaultPreset,
    };
}

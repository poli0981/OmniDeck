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

    /// <summary>The user's font-size slider, a factor on the preset's base size (0.85–1.4).</summary>
    public double FontScale { get; init; } = 1.0;

    /// <summary>Animations on or off; Windows' own "animation effects" switch can still turn them off.</summary>
    public bool MotionEnabled { get; init; } = true;

    /// <summary>An accent color as <c>#RRGGBB</c>; empty uses the Windows accent.</summary>
    public string AccentColor { get; init; } = "";

    internal const string DefaultPreset = "Standard";

    internal static readonly string[] KnownPresets = ["Dynamic", "Standard", "Comfort"];

    private static readonly System.Buffers.SearchValues<char> HexDigits = System.Buffers.SearchValues.Create("0123456789abcdefABCDEF");

    /// <summary>Repairs values a hand-edited or older file may carry.</summary>
    public OdSettings Normalized() => this with
    {
        SchemaVersion = CurrentSchemaVersion,
        Language = string.IsNullOrWhiteSpace(Language) ? "" : Language.Trim(),
        Theme = Enum.IsDefined(Theme) ? Theme : OdThemeMode.System,
        Preset = KnownPresets.FirstOrDefault(p => string.Equals(p, Preset, StringComparison.OrdinalIgnoreCase)) ?? DefaultPreset,
        FontScale = double.IsFinite(FontScale) ? Math.Clamp(FontScale, Theming.OdTokens.MinFontScale, Theming.OdTokens.MaxFontScale) : 1.0,
        AccentColor = IsHexColor(AccentColor) ? AccentColor.ToUpperInvariant() : "",
    };

    private static bool IsHexColor(string? value) =>
        value is { Length: 7 } && value[0] == '#' && !value.AsSpan(1).ContainsAnyExcept(HexDigits);

}

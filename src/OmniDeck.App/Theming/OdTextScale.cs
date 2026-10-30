using Microsoft.Win32;

namespace OmniDeck.App.Theming;

/// <summary>
/// The Windows "Text size" setting (Settings → Accessibility). WPF ignores it, so OmniDeck reads it
/// itself and multiplies the font tokens (docs/UI-MODES.md §4). The registry is read rather than
/// WinRT's UISettings, which would need a Windows-versioned target framework and its 26 MB projection
/// assembly (docs/spikes/05-theme-swap.md).
/// </summary>
internal static class OdTextScale
{
    private const string KeyPath = @"Software\Microsoft\Accessibility";

    public static double Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return FromRegistryValue(key?.GetValue("TextScaleFactor"));
    }

    /// <summary>The value is a percentage from 100 to 225; anything else means 100 %.</summary>
    internal static double FromRegistryValue(object? value) =>
        value is int percent and >= 100 and <= 225 ? percent / 100.0 : 1.0;
}

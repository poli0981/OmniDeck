using Microsoft.Win32;

namespace ThemeLab;

/// <summary>
/// Windows "Text size" (Settings → Accessibility). WPF ignores it, so OmniDeck has to read it and
/// multiply Od.Font.* itself. Two sources are compared: the registry (no WinRT projection needed)
/// and UISettings.TextScaleFactor (needs a Windows-versioned target framework).
/// </summary>
public static class TextScale
{
    public static double FromRegistry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Accessibility");
        return key?.GetValue("TextScaleFactor") is int percent ? percent / 100.0 : 1.0;
    }

    public static double FromWinRt(Windows.UI.ViewManagement.UISettings settings) => settings.TextScaleFactor;
}

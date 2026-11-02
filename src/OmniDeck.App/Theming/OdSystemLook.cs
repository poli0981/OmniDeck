using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace OmniDeck.App.Theming;

/// <summary>
/// The parts of Windows' look that OmniDeck follows: dark or light, the accent color, High Contrast
/// and animations. The theme is applied again only when one of them changes.
/// </summary>
internal readonly record struct OdSystemLook(SystemTheme Theme, Color Accent, bool HighContrast, bool Animations)
{
    public static OdSystemLook Read()
    {
        SystemThemeManager.UpdateSystemThemeCache();
        return new OdSystemLook(
            SystemThemeManager.GetCachedSystemTheme(),
            ApplicationAccentColorManager.GetColorizationColor(),
            SystemParameters.HighContrast,
            SystemParameters.ClientAreaAnimation);
    }
}

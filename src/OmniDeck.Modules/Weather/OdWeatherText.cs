using System.Globalization;
using OmniDeck.Core.Localization;

namespace OmniDeck.Modules.Weather;

/// <summary>Weather figures as the screen shows them, in the current culture.</summary>
internal static class OdWeatherText
{
    public static string Temperature(double value)
    {
        // Adding zero turns a rounded -0 into 0, which would otherwise show as "-0°".
        var rounded = Math.Round(value, MidpointRounding.AwayFromZero) + 0.0;
        return string.Create(CultureInfo.CurrentCulture, $"{rounded:0}°");
    }

    /// <summary>A percentage in the culture's format; empty when there is no figure.</summary>
    public static string Percent(int? value) => value is { } percent ? (percent / 100.0).ToString("P0", CultureInfo.CurrentCulture) : "";

    public static string Speed(double value, OdWeatherUnits units)
    {
        var rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        return units == OdWeatherUnits.Imperial
            ? OdLocalization.Format("Weather_SpeedImperial", rounded)
            : OdLocalization.Format("Weather_SpeedMetric", rounded);
    }

    /// <summary>The eight-point compass direction the wind blows from.</summary>
    public static string Compass(int degrees)
    {
        var point = (int)Math.Round((((degrees % 360) + 360) % 360) / 45.0, MidpointRounding.AwayFromZero) % 8;
        return point switch
        {
            0 => OdLocalization.Get("Weather_DirN"),
            1 => OdLocalization.Get("Weather_DirNE"),
            2 => OdLocalization.Get("Weather_DirE"),
            3 => OdLocalization.Get("Weather_DirSE"),
            4 => OdLocalization.Get("Weather_DirS"),
            5 => OdLocalization.Get("Weather_DirSW"),
            6 => OdLocalization.Get("Weather_DirW"),
            _ => OdLocalization.Get("Weather_DirNW"),
        };
    }
}

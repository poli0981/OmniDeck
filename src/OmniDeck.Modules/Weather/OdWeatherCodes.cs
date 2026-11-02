using OmniDeck.Core.Localization;
using Wpf.Ui.Controls;

namespace OmniDeck.Modules.Weather;

/// <summary>What Open-Meteo's WMO weather codes say, in words and as an icon.</summary>
internal static class OdWeatherCodes
{
    public static string Describe(int code) => code switch
    {
        0 => OdLocalization.Get("Weather_Clear"),
        1 => OdLocalization.Get("Weather_MainlyClear"),
        2 => OdLocalization.Get("Weather_PartlyCloudy"),
        3 => OdLocalization.Get("Weather_Overcast"),
        45 or 48 => OdLocalization.Get("Weather_Fog"),
        51 or 53 or 55 => OdLocalization.Get("Weather_Drizzle"),
        56 or 57 => OdLocalization.Get("Weather_FreezingDrizzle"),
        61 or 63 or 65 => OdLocalization.Get("Weather_Rain"),
        66 or 67 => OdLocalization.Get("Weather_FreezingRain"),
        71 or 73 or 75 or 77 => OdLocalization.Get("Weather_Snow"),
        80 or 81 or 82 => OdLocalization.Get("Weather_RainShowers"),
        85 or 86 => OdLocalization.Get("Weather_SnowShowers"),
        95 => OdLocalization.Get("Weather_Thunderstorm"),
        96 or 99 => OdLocalization.Get("Weather_ThunderstormHail"),
        _ => OdLocalization.Get("Weather_Unknown"),
    };

    public static SymbolRegular Symbol(int code, bool isDay) => code switch
    {
        0 or 1 => isDay ? SymbolRegular.WeatherSunny24 : SymbolRegular.WeatherMoon24,
        2 => isDay ? SymbolRegular.WeatherPartlyCloudyDay24 : SymbolRegular.WeatherPartlyCloudyNight24,
        3 => SymbolRegular.WeatherCloudy24,
        45 or 48 => SymbolRegular.WeatherFog24,
        51 or 53 or 55 or 56 or 57 => SymbolRegular.WeatherDrizzle24,
        61 or 63 or 65 or 66 or 67 => SymbolRegular.WeatherRain24,
        71 or 73 or 75 or 77 => SymbolRegular.WeatherSnow24,
        80 or 81 or 82 => isDay ? SymbolRegular.WeatherRainShowersDay24 : SymbolRegular.WeatherRainShowersNight24,
        85 or 86 => isDay ? SymbolRegular.WeatherSnowShowerDay24 : SymbolRegular.WeatherSnowShowerNight24,
        95 => SymbolRegular.WeatherThunderstorm24,
        96 or 99 => isDay ? SymbolRegular.WeatherHailDay24 : SymbolRegular.WeatherHailNight24,
        _ => SymbolRegular.WeatherCloudy24,
    };
}

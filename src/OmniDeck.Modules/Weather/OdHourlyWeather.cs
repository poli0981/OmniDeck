namespace OmniDeck.Modules.Weather;

/// <summary>One hour of the forecast, in the place's local time; the precipitation chance is a percentage, or null without a figure.</summary>
public sealed record OdHourlyWeather(DateTime Time, double Temperature, int? PrecipitationChance, int Code, bool IsDay);

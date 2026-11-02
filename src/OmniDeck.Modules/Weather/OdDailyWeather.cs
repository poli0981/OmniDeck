namespace OmniDeck.Modules.Weather;

/// <summary>One day of the forecast; the precipitation chance is a percentage, or null without a figure.</summary>
public sealed record OdDailyWeather(DateOnly Date, int Code, double High, double Low, int? PrecipitationChance);

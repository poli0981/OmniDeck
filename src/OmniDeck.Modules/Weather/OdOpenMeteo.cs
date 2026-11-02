using System.Globalization;
using System.Text.Json;

namespace OmniDeck.Modules.Weather;

/// <summary>
/// Open-Meteo's forecast and geocoding APIs (docs/SPEC.md §3): keyless, and the only weather service
/// docs/legal/PRIVACY.md lists. A request carries a saved place's coordinates or the name the user typed.
/// </summary>
internal static class OdOpenMeteo
{
    private const string Current = "temperature_2m,apparent_temperature,relative_humidity_2m,is_day,weather_code,wind_speed_10m,wind_direction_10m";
    private const string Hourly = "temperature_2m,precipitation_probability,weather_code,is_day";
    private const string Daily = "weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max";

    public static Uri SearchUri(string name, string language) => new(string.Create(
        CultureInfo.InvariantCulture,
        $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(name.Trim())}&count=8&language={Uri.EscapeDataString(language)}&format=json"));

    public static Uri ForecastUri(OdWeatherLocation location, OdWeatherUnits units)
    {
        var uri = string.Create(
            CultureInfo.InvariantCulture,
            $"https://api.open-meteo.com/v1/forecast?latitude={location.Latitude:F4}&longitude={location.Longitude:F4}&current={Current}&hourly={Hourly}&daily={Daily}&timezone=auto&forecast_days=7&forecast_hours=48");
        return new Uri(units == OdWeatherUnits.Imperial ? uri + "&temperature_unit=fahrenheit&wind_speed_unit=mph" : uri);
    }

    /// <summary>The cache key of a place's forecast in some units.</summary>
    public static string CacheKey(OdWeatherLocation location, OdWeatherUnits units) => location.Key + "|" + units;

    /// <summary>The places a geocoding answer lists; none when it lists no results.</summary>
    /// <exception cref="JsonException">The answer is not what Open-Meteo documents.</exception>
    public static IReadOnlyList<OdWeatherLocation> ParseSearch(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("results", out var results)
                ? [.. results.EnumerateArray().Select(Place)]
                : [];
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException)
        {
            throw new JsonException("Open-Meteo's geocoding answer is not in the expected shape.", ex);
        }
    }

    /// <summary>The forecast in a forecast answer, in the place's local time.</summary>
    /// <exception cref="JsonException">The answer is not what Open-Meteo documents.</exception>
    public static OdForecast ParseForecast(string json, OdWeatherUnits units)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return new OdForecast(CurrentFrom(root.GetProperty("current")), HoursFrom(root.GetProperty("hourly")), DaysFrom(root.GetProperty("daily")), units);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException)
        {
            throw new JsonException("Open-Meteo's forecast is not in the expected shape.", ex);
        }
    }

    private static OdWeatherLocation Place(JsonElement place) => new(
        place.GetProperty("name").GetString() ?? "",
        Text(place, "admin1"),
        Text(place, "country"),
        place.GetProperty("latitude").GetDouble(),
        place.GetProperty("longitude").GetDouble());

    private static OdCurrentWeather CurrentFrom(JsonElement now) => new(
        LocalTime(now.GetProperty("time")),
        now.GetProperty("temperature_2m").GetDouble(),
        now.GetProperty("apparent_temperature").GetDouble(),
        Whole(now.GetProperty("relative_humidity_2m")),
        Whole(now.GetProperty("weather_code")),
        Whole(now.GetProperty("is_day")) == 1,
        now.GetProperty("wind_speed_10m").GetDouble(),
        Whole(now.GetProperty("wind_direction_10m")));

    private static List<OdHourlyWeather> HoursFrom(JsonElement hourly)
    {
        var times = Column(hourly, "time");
        var temperatures = Column(hourly, "temperature_2m");
        var chances = Column(hourly, "precipitation_probability");
        var codes = Column(hourly, "weather_code");
        var days = Column(hourly, "is_day");
        var hours = new List<OdHourlyWeather>(times.Count);
        for (var i = 0; i < times.Count; i++)
        {
            hours.Add(new OdHourlyWeather(LocalTime(times[i]), temperatures[i].GetDouble(), Percent(chances[i]), Whole(codes[i]), Whole(days[i]) == 1));
        }

        return hours;
    }

    private static List<OdDailyWeather> DaysFrom(JsonElement daily)
    {
        var dates = Column(daily, "time");
        var codes = Column(daily, "weather_code");
        var highs = Column(daily, "temperature_2m_max");
        var lows = Column(daily, "temperature_2m_min");
        var chances = Column(daily, "precipitation_probability_max");
        var days = new List<OdDailyWeather>(dates.Count);
        for (var i = 0; i < dates.Count; i++)
        {
            var date = DateOnly.ParseExact(dates[i].GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            days.Add(new OdDailyWeather(date, Whole(codes[i]), highs[i].GetDouble(), lows[i].GetDouble(), Percent(chances[i])));
        }

        return days;
    }

    private static List<JsonElement> Column(JsonElement table, string name) => [.. table.GetProperty(name).EnumerateArray()];

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    // Open-Meteo writes whole numbers, but a JSON number may still come with a fraction.
    private static int Whole(JsonElement value) => (int)Math.Round(value.GetDouble(), MidpointRounding.AwayFromZero);

    private static int? Percent(JsonElement value) => value.ValueKind == JsonValueKind.Number ? Whole(value) : null;

    private static DateTime LocalTime(JsonElement value) =>
        DateTime.ParseExact(value.GetString()!, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
}

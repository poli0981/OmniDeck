using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using OmniDeck.Core.Storage;
using OmniDeck.Modules.Weather;
using OmniDeck.Tests.Localization;
using OmniDeck.Tests.TestSupport;
using Serilog.Core;
using Wpf.Ui.Controls;

namespace OmniDeck.Tests.ModuleApps;

// Weather texts follow the current culture, which the culture tests change.
[Collection(OdCultureCollection.Name)]
public sealed class OdWeatherTests : IDisposable
{
    // Trimmed from real Open-Meteo answers (2 November 2026, Hanoi).
    private const string Forecast = """
        {"latitude":21.05,"longitude":105.81,"utc_offset_seconds":25200,"timezone":"Asia/Bangkok",
         "current":{"time":"2026-11-02T08:45","interval":900,"temperature_2m":27.7,"apparent_temperature":33.1,"relative_humidity_2m":82,"is_day":1,"weather_code":51,"wind_speed_10m":6.5,"wind_direction_10m":67},
         "hourly":{"time":["2026-11-02T08:00","2026-11-02T09:00","2026-11-02T19:00"],"temperature_2m":[27.5,28.4,25.0],"precipitation_probability":[40,null,10],"weather_code":[51,2,0],"is_day":[1,1,0]},
         "daily":{"time":["2026-11-02","2026-11-03"],"weather_code":[95,80],"temperature_2m_max":[29.8,28.4],"temperature_2m_min":[24.1,23.0],"precipitation_probability_max":[100,69]}}
        """;

    private const string Search = """
        {"results":[{"id":1581130,"name":"Hanoi","latitude":21.0245,"longitude":105.84117,"country_code":"VN","timezone":"Asia/Bangkok","country":"Vietnam","admin1":"Hanoi"},
                    {"id":2,"name":"Hanoi","latitude":40.5,"longitude":-80.5,"country":"United States"}],"generationtime_ms":0.5}
        """;

    private static readonly OdWeatherLocation Hanoi = new("Hanoi", "Hanoi", "Vietnam", 21.0245, 105.84117);

    private readonly OdTempDirectory _temp = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 2, 1, 45, 0, TimeSpan.Zero));
    private readonly OdDatabase _database;

    public OdWeatherTests()
    {
        _database = new OdDatabase(_temp.Combine("omnideck.db"), pooling: false);
        _database.Migrate(OdCoreMigrations.All);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void AForecast_ParsesNow_TheHours_AndTheDays()
    {
        var forecast = OdOpenMeteo.ParseForecast(Forecast, OdWeatherUnits.Metric);

        Assert.Equal(new DateTime(2026, 11, 2, 8, 45, 0, DateTimeKind.Unspecified), forecast.Current.Time);
        Assert.Equal(27.7, forecast.Current.Temperature, 3);
        Assert.Equal(82, forecast.Current.Humidity);
        Assert.Equal(51, forecast.Current.Code);
        Assert.True(forecast.Current.IsDay);
        Assert.Equal(67, forecast.Current.WindDirection);
        Assert.Equal(3, forecast.Hourly.Count);
        Assert.Equal(40, forecast.Hourly[0].PrecipitationChance);
        Assert.Null(forecast.Hourly[1].PrecipitationChance);
        Assert.False(forecast.Hourly[2].IsDay);
        Assert.Equal(new OdDailyWeather(new DateOnly(2026, 11, 3), 80, 28.4, 23.0, 69), forecast.Daily[1]);
        Assert.Equal(OdWeatherUnits.Metric, forecast.Units);
    }

    [Theory]
    [InlineData("""{"current":{}}""")]
    [InlineData("""{"current":{"time":"later"}}""")]
    [InlineData("not json")]
    public void AnUnexpectedAnswer_IsAJsonException(string json) =>
        Assert.ThrowsAny<JsonException>(() => OdOpenMeteo.ParseForecast(json, OdWeatherUnits.Metric));

    [Fact]
    public void ASearch_ListsThePlaces_AndNoResultsIsAnEmptyList()
    {
        var places = OdOpenMeteo.ParseSearch(Search);

        Assert.Equal(2, places.Count);
        Assert.Equal(Hanoi, places[0]);
        Assert.Equal("Hanoi, Vietnam", places[0].Description);
        Assert.Equal("Hanoi, Vietnam", places[0].ToString());
        Assert.Equal("Hanoi, United States", places[1].Description);
        Assert.Empty(OdOpenMeteo.ParseSearch("""{"generationtime_ms":0.2}"""));
    }

    [Fact]
    public void Requests_CarryThePlaceOrTheName_AndTheUnits()
    {
        var metric = OdOpenMeteo.ForecastUri(Hanoi, OdWeatherUnits.Metric).AbsoluteUri;
        var imperial = OdOpenMeteo.ForecastUri(Hanoi, OdWeatherUnits.Imperial).AbsoluteUri;

        Assert.StartsWith("https://api.open-meteo.com/v1/forecast?latitude=21.0245&longitude=105.8412&", metric, StringComparison.Ordinal);
        Assert.DoesNotContain("fahrenheit", metric, StringComparison.Ordinal);
        Assert.EndsWith("&temperature_unit=fahrenheit&wind_speed_unit=mph", imperial, StringComparison.Ordinal);
        Assert.Equal(
            "https://geocoding-api.open-meteo.com/v1/search?name=Ho%20Chi%20Minh&count=8&language=vi&format=json",
            OdOpenMeteo.SearchUri(" Ho Chi Minh ", "vi").AbsoluteUri);
    }

    [Fact]
    public async Task AFreshForecast_ComesFromTheCache_AndAnOldOneIsFetchedAgain()
    {
        var http = new OdFakeHttp(_ => Ok(Forecast));
        var service = NewService(http);

        var first = await service.ForecastAsync(Hanoi, OdWeatherUnits.Metric, refresh: false, Token);
        _time.Advance(TimeSpan.FromMinutes(29));
        var cached = await service.ForecastAsync(Hanoi, OdWeatherUnits.Metric, refresh: false, Token);
        _time.Advance(TimeSpan.FromMinutes(2));
        var fetched = await service.ForecastAsync(Hanoi, OdWeatherUnits.Metric, refresh: false, Token);
        var refreshed = await service.ForecastAsync(Hanoi, OdWeatherUnits.Metric, refresh: true, Token);

        Assert.Equal(3, http.Requests.Count);
        Assert.False(first!.IsStale);
        Assert.Equal(first.FetchedAt, cached!.FetchedAt);
        Assert.Equal(_time.GetUtcNow(), fetched!.FetchedAt);
        Assert.False(refreshed!.IsStale);
    }

    [Fact]
    public async Task WhenOpenMeteoIsOutOfReach_TheLastForecastIsShownAsStale()
    {
        var reachable = true;
        var service = NewService(new OdFakeHttp(_ => reachable ? Ok(Forecast) : throw new HttpRequestException("No such host is known.")));
        var first = await service.ForecastAsync(Hanoi, OdWeatherUnits.Metric, refresh: false, Token);
        reachable = false;
        _time.Advance(TimeSpan.FromHours(2));

        var report = await service.ForecastAsync(Hanoi, OdWeatherUnits.Metric, refresh: false, Token);

        Assert.True(report!.IsStale);
        Assert.Equal(first!.FetchedAt, report.FetchedAt);
    }

    [Fact]
    public async Task WithNothingCached_AFailureLeavesNothingToShow()
    {
        var service = NewService(new OdFakeHttp(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        Assert.Null(await service.ForecastAsync(Hanoi, OdWeatherUnits.Metric, refresh: false, Token));
    }

    [Fact]
    public async Task EachUnitIsCachedApart_AndForgettingAPlaceForgetsThemAll()
    {
        var http = new OdFakeHttp(_ => Ok(Forecast));
        var service = NewService(http);

        await service.ForecastAsync(Hanoi, OdWeatherUnits.Metric, refresh: false, Token);
        await service.ForecastAsync(Hanoi, OdWeatherUnits.Imperial, refresh: false, Token);
        service.Forget(Hanoi);
        await service.ForecastAsync(Hanoi, OdWeatherUnits.Metric, refresh: false, Token);

        Assert.Equal(3, http.Requests.Count);
        Assert.Contains("fahrenheit", http.Requests[1].RequestUri!.Query, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, true, SymbolRegular.WeatherSunny24)]
    [InlineData(0, false, SymbolRegular.WeatherMoon24)]
    [InlineData(2, false, SymbolRegular.WeatherPartlyCloudyNight24)]
    [InlineData(63, true, SymbolRegular.WeatherRain24)]
    [InlineData(81, false, SymbolRegular.WeatherRainShowersNight24)]
    [InlineData(96, false, SymbolRegular.WeatherHailNight24)]
    [InlineData(42, true, SymbolRegular.WeatherCloudy24)]
    public void Codes_HaveAnIcon(int code, bool isDay, SymbolRegular symbol) =>
        Assert.Equal(symbol, OdWeatherCodes.Symbol(code, isDay));

    [Fact]
    public void Codes_AreDescribedInWords()
    {
        Assert.Equal("Clear sky", OdWeatherCodes.Describe(0));
        Assert.Equal("Drizzle", OdWeatherCodes.Describe(53));
        Assert.Equal("Thunderstorm with hail", OdWeatherCodes.Describe(99));
        Assert.Equal("Unknown conditions", OdWeatherCodes.Describe(42));
    }

    [Theory]
    [InlineData(0, "N")]
    [InlineData(22, "N")]
    [InlineData(23, "NE")]
    [InlineData(67, "NE")]
    [InlineData(180, "S")]
    [InlineData(225, "SW")]
    [InlineData(350, "N")]
    [InlineData(-90, "W")]
    public void WindDirections_RoundToEightPoints(int degrees, string point) =>
        Assert.Equal(point, OdWeatherText.Compass(degrees));

    [Fact]
    public void Figures_FollowTheCulture()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            Assert.Equal("28°", OdWeatherText.Temperature(27.5));
            Assert.Equal("0°", OdWeatherText.Temperature(-0.4));
            Assert.Equal("-3°", OdWeatherText.Temperature(-2.6));
            Assert.Equal("40%", OdWeatherText.Percent(40));
            Assert.Equal("", OdWeatherText.Percent(null));
            Assert.Equal("7 km/h", OdWeatherText.Speed(6.5, OdWeatherUnits.Metric));
            Assert.Equal("4 mph", OdWeatherText.Speed(4.2, OdWeatherUnits.Imperial));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void AddingAPlace_SavesIt_SelectsIt_AndShowsItsForecast() => OdSta.Run(async () =>
    {
        var saved = new List<OdWeatherSettings>();
        var http = new OdFakeHttp(request => Ok(request.RequestUri!.Host.StartsWith("geocoding", StringComparison.Ordinal) ? Search : Forecast));
        using var weather = new OdWeatherViewModel(new OdWeatherSettings(), saved.Add, NewService(http), _time);
        Assert.True(weather.IsEmpty);

        weather.SearchText = "Hanoi";
        await weather.SearchCommand.ExecuteAsync(null);
        weather.AddCommand.Execute(weather.SearchResults[0]);
        await weather.ShowAsync();

        Assert.Equal(Hanoi, Assert.Single(weather.Locations));
        Assert.Equal(Hanoi, weather.SelectedLocation);
        Assert.Equal(Hanoi, Assert.Single(saved[^1].Locations));
        Assert.Empty(weather.SearchResults);
        Assert.Equal("", weather.SearchText);
        Assert.True(weather.HasForecast);
        Assert.Equal(3, weather.Hours.Count);
        Assert.Equal(2, weather.Days.Count);
        Assert.Equal("Drizzle", weather.Now!.Condition);
        Assert.False(weather.HasStatus);
        Assert.NotNull(weather.Updated);
    });

    [Fact]
    public void RemovingTheShownPlace_ShowsTheNextOne_ThenNothing() => OdSta.Run(async () =>
    {
        var saigon = new OdWeatherLocation("Ho Chi Minh City", "", "Vietnam", 10.8231, 106.6297);
        var saved = new List<OdWeatherSettings>();
        using var weather = new OdWeatherViewModel(new OdWeatherSettings { Locations = [Hanoi, saigon], Selected = 1 }, saved.Add, NewService(new OdFakeHttp(_ => Ok(Forecast))), _time);
        Assert.Equal(saigon, weather.SelectedLocation);
        Assert.Empty(saved);

        weather.RemoveCommand.Execute(saigon);
        await weather.ShowAsync();
        Assert.Equal(Hanoi, weather.SelectedLocation);
        weather.RemoveCommand.Execute(Hanoi);
        await weather.ShowAsync();

        Assert.Null(weather.SelectedLocation);
        Assert.True(weather.IsEmpty);
        Assert.False(weather.HasForecast);
        Assert.Empty(saved[^1].Locations);
    });

    [Fact]
    public void ChoosingUnits_IsSaved_AndFetchesInThoseUnits() => OdSta.Run(async () =>
    {
        var saved = new List<OdWeatherSettings>();
        var http = new OdFakeHttp(_ => Ok(Forecast));
        using var weather = new OdWeatherViewModel(new OdWeatherSettings { Locations = [Hanoi] }, saved.Add, NewService(http), _time);

        weather.SelectedUnits = weather.UnitsOptions[2];
        await weather.ShowAsync();

        Assert.Equal(OdWeatherUnits.Imperial, weather.Units);
        Assert.Equal(OdWeatherUnits.Imperial, saved[^1].Units);
        Assert.Contains("fahrenheit", http.Requests[^1].RequestUri!.Query, StringComparison.Ordinal);
    });

    [Fact]
    public void WithOpenMeteoOutOfReach_TheScreenSaysSo() => OdSta.Run(async () =>
    {
        var service = NewService(new OdFakeHttp(_ => throw new HttpRequestException("offline")));
        using var weather = new OdWeatherViewModel(new OdWeatherSettings { Locations = [Hanoi] }, _ => { }, service, _time);

        await weather.ShowAsync();
        weather.SearchText = "Hanoi";
        await weather.SearchCommand.ExecuteAsync(null);

        Assert.True(weather.HasStatus);
        Assert.False(weather.HasForecast);
        Assert.True(weather.HasSearchMessage);
        Assert.Empty(weather.SearchResults);
    });

    [Fact]
    public void ASearchWithNoMatch_SaysSo() => OdSta.Run(async () =>
    {
        var service = NewService(new OdFakeHttp(_ => Ok("""{"generationtime_ms":0.2}""")));
        using var weather = new OdWeatherViewModel(new OdWeatherSettings(), _ => { }, service, _time);

        weather.SearchText = "Nowhere at all";
        await weather.SearchCommand.ExecuteAsync(null);

        Assert.Equal("No places match that name.", weather.SearchMessage);
    });

    [Fact]
    public void TheNextHoursChart_SumsTheHoursUp_AndReadsTheOneUnderTheCursor() => OdSta.Run(async () =>
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            using var weather = new OdWeatherViewModel(new OdWeatherSettings { Locations = [Hanoi] }, _ => { }, NewService(new OdFakeHttp(_ => Ok(Forecast))), _time);
            string At(int hour) => new DateTime(2026, 11, 2, hour, 0, 0, DateTimeKind.Unspecified).ToString("t", CultureInfo.CurrentCulture);

            await weather.ShowAsync();

            Assert.Equal(3, weather.HourlyPoints.Count);
            Assert.Equal(28.4, weather.HourlyPoints[1].Line, 3);
            Assert.Null(weather.HourlyPoints[1].Bar);
            Assert.Equal($"Next 48 hours: from 25° to 28°; rain is most likely on Monday at {At(8)} (40%).", weather.HourlySummary);
            Assert.Equal("", weather.HourlyReadout);

            weather.HourlyCursor = 2;
            Assert.Equal($"Monday at {At(19)}: 25°, Clear sky, 10% chance of rain", weather.HourlyReadout);
            weather.HourlyCursor = -1;
            Assert.Equal("", weather.HourlyReadout);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    });

    private static HttpResponseMessage Ok(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json) };

    private OdWeatherService NewService(HttpMessageHandler handler) =>
        new(new HttpClient(handler), new OdWeatherCache(_database), _time, Logger.None);
}

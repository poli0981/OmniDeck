using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmniDeck.Core.Localization;
using OmniDeck.Modules.Charts;

namespace OmniDeck.Modules.Weather;

/// <summary>
/// The weather screen: the saved places, the search that adds one, and the forecast for the place on
/// screen. Forecasts are fetched only while the module is shown, through the cache, so a hidden
/// module costs no network at all.
/// </summary>
public sealed partial class OdWeatherViewModel : ObservableObject, IDisposable
{
    private readonly OdWeatherService _service;
    private readonly Action<OdWeatherSettings> _save;
    private readonly TimeProvider _time;
    private readonly bool _restoring;
    private CancellationTokenSource _work = new();

    public OdWeatherViewModel(OdWeatherSettings settings, Action<OdWeatherSettings> save, OdWeatherService service, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _service = service;
        _save = save;
        _time = time;
        UnitsOptions =
        [
            new(null, () => OdLocalization.Get("Weather_UnitsAuto")),
            new(OdWeatherUnits.Metric, () => OdLocalization.Get("Weather_UnitsMetric")),
            new(OdWeatherUnits.Imperial, () => OdLocalization.Get("Weather_UnitsImperial")),
        ];
        Locations = [.. settings.Locations];

        // Restoring the saved choices must not save them again or start a fetch.
        _restoring = true;
        SelectedUnits = UnitsOptions.FirstOrDefault(option => option.Units == settings.Units) ?? UnitsOptions[0];
        var selected = settings.Selected >= 0 && settings.Selected < Locations.Count ? settings.Selected : 0;
        SelectedLocation = Locations.Count == 0 ? null : Locations[selected];
        _restoring = false;
        OdLocalization.FollowCulture(OnCultureChanged);
    }

    public ObservableCollection<OdWeatherLocation> Locations { get; }

    public ObservableCollection<OdWeatherLocation> SearchResults { get; } = [];

    public ObservableCollection<OdWeatherHour> Hours { get; } = [];

    public ObservableCollection<OdWeatherDay> Days { get; } = [];

    public IReadOnlyList<OdWeatherUnitsOption> UnitsOptions { get; }

    public bool HasLocations => Locations.Count > 0;

    public bool IsEmpty => Locations.Count == 0;

    public bool HasForecast => Now is not null;

    public bool HasStatus => Status is not null;

    public bool HasSearchMessage => SearchMessage is not null;

    /// <summary>The units forecasts come in: the user's choice, or the region's.</summary>
    public OdWeatherUnits Units => SelectedUnits?.Units ?? (RegionInfo.CurrentRegion.IsMetric ? OdWeatherUnits.Metric : OdWeatherUnits.Imperial);

    [ObservableProperty]
    private OdWeatherLocation? _selectedLocation;

    [ObservableProperty]
    private OdWeatherUnitsOption? _selectedUnits;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearchMessage))]
    private string? _searchMessage;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasForecast))]
    private OdWeatherNow? _now;

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Why the forecast is old or missing; null when it is current.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string? _status;

    [ObservableProperty]
    private string? _updated;

    /// <summary>The next hours for the chart: the temperature as the line, the chance of rain as the bars.</summary>
    [ObservableProperty]
    private IReadOnlyList<OdChartPoint> _hourlyPoints = [];

    /// <summary>The next hours in one sentence: the chart's name for screen readers.</summary>
    [ObservableProperty]
    private string _hourlySummary = "";

    /// <summary>The hour the chart's cursor is on, or -1.</summary>
    [ObservableProperty]
    private int _hourlyCursor = -1;

    /// <summary>The hour under the chart's cursor, in words.</summary>
    [ObservableProperty]
    private string _hourlyReadout = "";

    private IReadOnlyList<OdHourlyWeather> _hourly = [];

    /// <summary>Shows the forecast for the place on screen; the cache answers while it is fresh.</summary>
    public Task ShowAsync() => LoadAsync(refresh: false);

    /// <summary>Stops the fetch or search in progress, as when the module is suspended.</summary>
    public void Cancel()
    {
        _work.Cancel();
        _work.Dispose();
        _work = new CancellationTokenSource();
    }

    public void Dispose()
    {
        _work.Cancel();
        _work.Dispose();
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync(refresh: true);

    [RelayCommand]
    private async Task SearchAsync()
    {
        var name = SearchText.Trim();
        SearchResults.Clear();
        SearchMessage = null;
        if (name.Length < 2)
        {
            return;
        }

        var token = _work.Token;
        IsSearching = true;
        try
        {
            var found = await _service.SearchAsync(name, OdLocalization.Instance.Culture.TwoLetterISOLanguageName, token);
            foreach (var place in found)
            {
                SearchResults.Add(place);
            }

            SearchMessage = found.Count == 0 ? OdLocalization.Get("Weather_SearchNone") : null;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The module was hidden or closed while searching.
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            SearchMessage = OdLocalization.Get("Weather_SearchFailed");
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private void Add(OdWeatherLocation place)
    {
        var saved = Locations.FirstOrDefault(location => string.Equals(location.Key, place.Key, StringComparison.Ordinal));
        if (saved is null)
        {
            saved = place;
            Locations.Add(place);
            NotifyLocationsChanged();
        }

        SearchResults.Clear();
        SearchText = "";
        SearchMessage = null;
        if (SelectedLocation == saved)
        {
            Save();
        }
        else
        {
            SelectedLocation = saved;
        }
    }

    [RelayCommand]
    private void Remove(OdWeatherLocation place)
    {
        var index = Locations.IndexOf(place);
        if (index < 0)
        {
            return;
        }

        Locations.RemoveAt(index);
        _service.Forget(place);
        NotifyLocationsChanged();
        if (SelectedLocation == place)
        {
            SelectedLocation = Locations.Count == 0 ? null : Locations[Math.Min(index, Locations.Count - 1)];
        }
        else
        {
            Save();
        }
    }

    partial void OnSelectedLocationChanged(OdWeatherLocation? value) => Rechoose();

    partial void OnSelectedUnitsChanged(OdWeatherUnitsOption? value) => Rechoose();

    partial void OnHourlyCursorChanged(int value) =>
        HourlyReadout = value >= 0 && value < _hourly.Count ? Readout(_hourly[value]) : "";

    private static string Readout(OdHourlyWeather hour) => OdLocalization.Format("Weather_ChartReadout",
        hour.Time.ToString("dddd", CultureInfo.CurrentCulture),
        hour.Time.ToString("t", CultureInfo.CurrentCulture),
        OdWeatherText.Temperature(hour.Temperature),
        OdWeatherCodes.Describe(hour.Code),
        OdWeatherText.Percent(hour.PrecipitationChance));

    private static string Summarize(IReadOnlyList<OdHourlyWeather> hours)
    {
        if (hours.Count == 0)
        {
            return "";
        }

        var low = OdWeatherText.Temperature(hours.Min(hour => hour.Temperature));
        var high = OdWeatherText.Temperature(hours.Max(hour => hour.Temperature));
        return hours.Where(hour => hour.PrecipitationChance > 0).MaxBy(hour => hour.PrecipitationChance) is { } wettest
            ? OdLocalization.Format("Weather_ChartSummary",
                low,
                high,
                wettest.Time.ToString("dddd", CultureInfo.CurrentCulture),
                wettest.Time.ToString("t", CultureInfo.CurrentCulture),
                OdWeatherText.Percent(wettest.PrecipitationChance))
            : OdLocalization.Format("Weather_ChartSummaryDry", low, high);
    }

    private void Rechoose()
    {
        if (_restoring)
        {
            return;
        }

        Save();
        _ = LoadAsync(refresh: false);
    }

    private void NotifyLocationsChanged()
    {
        OnPropertyChanged(nameof(HasLocations));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void Save() => _save(new OdWeatherSettings
    {
        Locations = [.. Locations],
        Selected = SelectedLocation is { } selected ? Math.Max(0, Locations.IndexOf(selected)) : 0,
        Units = SelectedUnits?.Units,
    });

    private async Task LoadAsync(bool refresh)
    {
        if (SelectedLocation is not { } place)
        {
            Show(null);
            Status = null;
            return;
        }

        var token = _work.Token;
        IsLoading = true;
        try
        {
            var report = await _service.ForecastAsync(place, Units, refresh, token);
            if (place != SelectedLocation)
            {
                return;
            }

            Show(report?.Forecast);
            if (report is null)
            {
                Updated = null;
                Status = OdLocalization.Get("Weather_Unavailable");
                return;
            }

            var fetched = TimeZoneInfo.ConvertTime(report.FetchedAt, _time.LocalTimeZone);
            Updated = OdLocalization.Format("Weather_Updated", fetched.ToString("t", CultureInfo.CurrentCulture));
            Status = report.IsStale ? OdLocalization.Format("Weather_Stale", fetched.ToString("g", CultureInfo.CurrentCulture)) : null;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The module was hidden or closed while fetching.
        }
        finally
        {
            IsLoading = false;
        }
    }

    // A change of language: the units' names, and the forecast shown again, from the cache.
    private void OnCultureChanged(object? sender, EventArgs e)
    {
        foreach (var option in UnitsOptions)
        {
            option.RefreshLabel();
        }

        _ = LoadAsync(refresh: false);
    }

    private void Show(OdForecast? forecast)
    {
        Hours.Clear();
        Days.Clear();
        HourlyCursor = -1;
        _hourly = forecast?.Hourly ?? [];
        HourlyPoints = [.. _hourly.Select(hour => new OdChartPoint(hour.Time, hour.Temperature, hour.PrecipitationChance))];
        HourlySummary = Summarize(_hourly);
        if (forecast is null)
        {
            Now = null;
            return;
        }

        var current = forecast.Current;
        Now = new OdWeatherNow(
            OdWeatherText.Temperature(current.Temperature),
            OdWeatherCodes.Describe(current.Code),
            OdWeatherCodes.Symbol(current.Code, current.IsDay),
            OdLocalization.Format("Weather_FeelsLike", OdWeatherText.Temperature(current.FeelsLike)),
            OdLocalization.Format("Weather_Humidity", OdWeatherText.Percent(current.Humidity)),
            OdLocalization.Format("Weather_Wind", OdWeatherText.Speed(current.WindSpeed, forecast.Units), OdWeatherText.Compass(current.WindDirection)));

        foreach (var hour in forecast.Hourly)
        {
            Hours.Add(new OdWeatherHour(
                hour.Time.ToString("t", CultureInfo.CurrentCulture),
                OdWeatherText.Temperature(hour.Temperature),
                OdWeatherCodes.Symbol(hour.Code, hour.IsDay),
                OdWeatherCodes.Describe(hour.Code),
                OdWeatherText.Percent(hour.PrecipitationChance)));
        }

        var today = DateOnly.FromDateTime(current.Time);
        foreach (var day in forecast.Daily)
        {
            Days.Add(new OdWeatherDay(
                day.Date == today ? OdLocalization.Get("Weather_Today") : day.Date.ToString(OdLocalization.Get("Weather_DayFormat"), CultureInfo.CurrentCulture),
                OdLocalization.Format("Weather_HighLow", OdWeatherText.Temperature(day.High), OdWeatherText.Temperature(day.Low)),
                OdWeatherCodes.Symbol(day.Code, isDay: true),
                OdWeatherCodes.Describe(day.Code),
                OdWeatherText.Percent(day.PrecipitationChance)));
        }
    }
}

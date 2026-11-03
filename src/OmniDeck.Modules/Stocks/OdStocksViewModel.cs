using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmniDeck.Core.Localization;
using OmniDeck.Core.Security;
using Wpf.Ui.Controls;

namespace OmniDeck.Modules.Stocks;

/// <summary>
/// The Stocks screen (docs/SPEC.md §3.5): the Twelve Data key's setup, the watchlist, the search that
/// adds to it, and what the key's credits allow. It refreshes only while on screen, through the cache
/// and the credit limiter; whatever goes wrong, the last figures stay.
/// </summary>
public sealed partial class OdStocksViewModel : ObservableObject, IDisposable
{
    /// <summary>The key's name in the secret store.</summary>
    public const string SecretName = "twelvedata";

    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(1);

    private readonly Action<OdStocksSettings> _save;
    private readonly IOdSecretStore _secrets;
    private readonly IOdQuoteProvider _provider;
    private readonly OdStocksCache _cache;
    private readonly OdCreditLimiter _limiter;
    private readonly OdStocksRefresher _refresher;
    private readonly TimeProvider _time;

    // Listings that need a higher plan or do not exist: not asked for again this session.
    private readonly Dictionary<string, OdStocksError> _unavailable = new(StringComparer.Ordinal);
    private OdStocksSettings _settings;
    private CancellationTokenSource _work = new();
    private ITimer? _ticker;
    private OdRefreshResult? _lastResult;
    private DateTimeOffset? _nextAttempt;
    private bool _refreshing;

    public OdStocksViewModel(OdStocksSettings settings, Action<OdStocksSettings> save, IOdSecretStore secrets, IOdQuoteProvider provider, OdStocksCache cache, OdCreditLimiter limiter, TimeProvider time, Serilog.ILogger log)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(secrets);
        ArgumentNullException.ThrowIfNull(limiter);
        _settings = settings;
        _save = save;
        _secrets = secrets;
        _provider = provider;
        _cache = cache;
        _limiter = limiter;
        _time = time;
        _refresher = new OdStocksRefresher(provider, cache, limiter, time, log);
        limiter.SetLimits(settings.MinuteLimit, settings.DayLimit);
        Rows = [.. settings.Watchlist.Select(item => new OdWatchRowViewModel(item))];
        HasKey = secrets.Read(SecretName) is not null;
        ShowRows();
        ShowCredits();
        OdLocalization.FollowCulture(OnCultureChanged);
    }

    public ObservableCollection<OdWatchRowViewModel> Rows { get; }

    public ObservableCollection<OdListingRowViewModel> SearchResults { get; } = [];

    /// <summary>The setup shows until a key is saved, and again while the user changes it.</summary>
    public bool ShowsSetup => !HasKey || IsEditingKey;

    public bool ShowsWatchlist => HasKey && !IsEditingKey;

    public bool IsEmpty => Rows.Count == 0;

    public bool HasStatus => Status is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsSetup), nameof(ShowsWatchlist))]
    private bool _hasKey;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsSetup), nameof(ShowsWatchlist))]
    private bool _isEditingKey;

    [ObservableProperty]
    private string _keyDraft = "";

    [ObservableProperty]
    private string? _keyMessage;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string? _searchMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string? _status;

    [ObservableProperty]
    private InfoBarSeverity _statusSeverity = InfoBarSeverity.Informational;

    [ObservableProperty]
    private string _credits = "";

    /// <summary>Says so when quotes refresh less often than chosen, so the day's credits last.</summary>
    [ObservableProperty]
    private string? _intervalNote;

    [ObservableProperty]
    private string _plan = "";

    /// <summary>The watchlist row on screen in detail, if any.</summary>
    [ObservableProperty]
    private OdWatchRowViewModel? _selectedRow;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetail))]
    private OdStockDetailViewModel? _detail;

    public bool HasDetail => Detail is not null;

    /// <summary>On screen: refreshes what is stale now, then checks every minute.</summary>
    public void Activate()
    {
        var context = SynchronizationContext.Current;
        _ticker ??= _time.CreateTimer(_ => context?.Post(_ => OnTick(), null), null, Tick, Tick);
        _ = RefreshAsync(force: false);
    }

    /// <summary>Off screen: no more refreshes, and the one under way stops.</summary>
    public void Deactivate()
    {
        _ticker?.Dispose();
        _ticker = null;
        _work.Cancel();
        _work.Dispose();
        _work = new CancellationTokenSource();
    }

    public void Dispose()
    {
        OdLocalization.UnfollowCulture(OnCultureChanged);
        _ticker?.Dispose();
        _ticker = null;
        _work.Cancel();
        _work.Dispose();
    }

    /// <summary>Refreshes the stale quotes, or every one when <paramref name="force"/>; the tests await it.</summary>
    internal async Task RefreshAsync(bool force)
    {
        var tickers = Rows.Select(row => row.Item.Ticker).Where(ticker => !_unavailable.ContainsKey(ticker.Key)).ToList();
        if (_refreshing || tickers.Count == 0 || !HasKey)
        {
            return;
        }

        if (_secrets.Read(SecretName) is not { } key)
        {
            // The stored key no longer decrypts (a profile moved to another PC): ask for it again.
            HasKey = false;
            return;
        }

        var token = _work.Token;
        _refreshing = true;
        IsBusy = true;
        try
        {
            var chosen = TimeSpan.FromMinutes(Math.Max(1, _settings.RefreshMinutes));
            var interval = OdStocksRefresher.IntervalFor(tickers.Count, chosen, tickers[0], _time.GetUtcNow(), _limiter.LeftToday);
            IntervalNote = interval > chosen ? OdLocalization.Format("Stocks_IntervalStretched", Math.Round(interval.TotalMinutes)) : null;
            var result = await _refresher.RefreshAsync(tickers, key, force, interval, token);
            foreach (var (ticker, error) in result.Failed.Where(failure => failure.Value is OdStocksError.PlanRequired or OdStocksError.NotFound))
            {
                _unavailable[ticker] = error;
            }

            _nextAttempt = result.NextAttempt;
            _lastResult = result;
            ShowResult(result);
            ShowRows();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Left the screen while refreshing.
        }
        finally
        {
            _refreshing = false;
            IsBusy = false;
            ShowCredits();
        }
    }

    [RelayCommand]
    private async Task TestKeyAsync()
    {
        var key = KeyDraft.Trim();
        if (key.Length == 0)
        {
            KeyMessage = OdLocalization.Get("Stocks_KeyEmpty");
            return;
        }

        // Testing costs a credit, and the limiter says whether one can go now.
        if (_limiter.WaitFor(1) != TimeSpan.Zero)
        {
            KeyMessage = OdLocalization.Get("Stocks_KeyBusy");
            return;
        }

        IsBusy = true;
        KeyMessage = null;
        try
        {
            _limiter.Spend(1);
            var usage = await _provider.UsageAsync(key, _work.Token);
            _secrets.Write(SecretName, key);
            _limiter.SetLimits(usage.MinuteLimit, usage.DayLimit);
            _limiter.Sync(usage.DayUsed);
            Save(_settings with { Plan = usage.Plan, MinuteLimit = usage.MinuteLimit, DayLimit = usage.DayLimit });
            KeyDraft = "";
            _unavailable.Clear();
            HasKey = true;
            IsEditingKey = false;
            Status = null;
        }
        catch (OdStocksException ex)
        {
            KeyMessage = ex.Error switch
            {
                OdStocksError.KeyRejected => OdLocalization.Get("Stocks_KeyRejected"),
                OdStocksError.Offline => OdLocalization.Get("Stocks_KeyOffline"),
                OdStocksError.MinuteLimit or OdStocksError.DayLimit => OdLocalization.Get("Stocks_KeyBusy"),
                _ => OdLocalization.Get("Stocks_KeyFailed"),
            };
            return;
        }
        finally
        {
            IsBusy = false;
            ShowCredits();
        }

        await RefreshAsync(force: true);
    }

    [RelayCommand]
    private void ChangeKey()
    {
        KeyDraft = "";
        KeyMessage = null;
        IsEditingKey = true;
    }

    [RelayCommand]
    private void CancelKey()
    {
        KeyDraft = "";
        KeyMessage = null;
        IsEditingKey = false;
    }

    /// <summary>Forgets the key; the watchlist and its last figures stay.</summary>
    [RelayCommand]
    private void RemoveKey()
    {
        _secrets.Delete(SecretName);
        HasKey = false;
        IsEditingKey = false;
        Status = null;
    }

    [RelayCommand]
    private Task RefreshNowAsync() => RefreshAsync(force: true);

    [RelayCommand]
    private async Task SearchAsync()
    {
        var text = SearchText.Trim();
        SearchResults.Clear();
        SearchMessage = null;
        if (text.Length == 0 || _secrets.Read(SecretName) is not { } key)
        {
            return;
        }

        var token = _work.Token;
        IsBusy = true;
        try
        {
            var listings = await _provider.SearchAsync(text, key, token);
            foreach (var listing in listings.Take(10))
            {
                var added = Rows.Any(row => row.Item.Ticker == listing.Ticker);
                SearchResults.Add(new OdListingRowViewModel(listing, OdStocksPlans.Covers(_settings.Plan, listing.Plan), added));
            }

            SearchMessage = listings.Count == 0 ? OdLocalization.Get("Stocks_SearchNone") : null;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Left the screen while searching.
        }
        catch (OdStocksException)
        {
            SearchMessage = OdLocalization.Get("Stocks_SearchFailed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddAsync(OdListingRowViewModel result)
    {
        if (!result.CanAdd)
        {
            return;
        }

        var listing = result.Listing;
        Rows.Add(new OdWatchRowViewModel(new OdWatchItem(listing.Symbol, listing.Mic, listing.Name, listing.Currency)));
        OnPropertyChanged(nameof(IsEmpty));
        SaveWatchlist();
        SearchResults.Clear();
        SearchText = "";
        ShowRows();
        await RefreshAsync(force: false);
    }

    [RelayCommand]
    private void Remove(OdWatchRowViewModel? row)
    {
        if (row is null || !Rows.Remove(row))
        {
            return;
        }

        _cache.Forget(row.Item.Ticker);
        _unavailable.Remove(row.Item.Ticker.Key);
        if (SelectedRow == row)
        {
            SelectedRow = null;
        }

        OnPropertyChanged(nameof(IsEmpty));
        SaveWatchlist();
    }

    [RelayCommand]
    private void CloseDetail() => SelectedRow = null;

    /// <summary>
    /// A series for the detail view: the cached one while it is fresh, otherwise fetched, one credit,
    /// since the user asked for it; the cached one again, however old, when that cannot be done.
    /// </summary>
    internal async Task<OdSeries?> LoadSeriesAsync(OdTicker ticker, OdSeriesRange range)
    {
        var cached = _cache.Series(ticker, range);
        if (cached is not null && OdStocksFreshness.IsFresh(cached, _time.GetUtcNow()))
        {
            return cached.Value;
        }

        if (_unavailable.ContainsKey(ticker.Key) || _secrets.Read(SecretName) is not { } key || _limiter.WaitFor(1) != TimeSpan.Zero)
        {
            return cached?.Value;
        }

        _limiter.Spend(1);
        try
        {
            var series = await _provider.SeriesAsync(ticker, range, key, _work.Token);
            _cache.PutSeries(series, _time.GetUtcNow());
            return series;
        }
        catch (OdStocksException)
        {
            return cached?.Value;
        }
        catch (OperationCanceledException)
        {
            return cached?.Value;
        }
        finally
        {
            ShowCredits();
        }
    }

    partial void OnSelectedRowChanged(OdWatchRowViewModel? value)
    {
        if (value is null)
        {
            Detail = null;
            return;
        }

        var ticker = value.Item.Ticker;
        Detail = new OdStockDetailViewModel(value.Item, _cache.Quote(ticker)?.Value, range => LoadSeriesAsync(ticker, range));
        _ = Detail.LoadAsync();
    }

    [RelayCommand]
    private void MoveUp(OdWatchRowViewModel? row) => Move(row, -1);

    [RelayCommand]
    private void MoveDown(OdWatchRowViewModel? row) => Move(row, 1);

    private void Move(OdWatchRowViewModel? row, int by)
    {
        var index = row is null ? -1 : Rows.IndexOf(row);
        var target = index + by;
        if (index < 0 || target < 0 || target >= Rows.Count)
        {
            return;
        }

        Rows.Move(index, target);
        SaveWatchlist();
    }

    private void OnTick()
    {
        if (_nextAttempt is { } next && _time.GetUtcNow() < next)
        {
            return;
        }

        _ = RefreshAsync(force: false);
    }

    private void ShowRows()
    {
        foreach (var row in Rows)
        {
            var ticker = row.Item.Ticker;
            var note = _unavailable.GetValueOrDefault(ticker.Key) switch
            {
                OdStocksError.PlanRequired => OdLocalization.Get("Stocks_NotOnPlan"),
                OdStocksError.NotFound => OdLocalization.Get("Stocks_NotFound"),
                _ => "",
            };
            row.Show(_cache.Quote(ticker)?.Value, _cache.Series(ticker, OdSeriesRange.Daily)?.Value, note, _time);
        }
    }

    private void ShowResult(OdRefreshResult result)
    {
        var local = (DateTimeOffset when) => TimeZoneInfo.ConvertTime(when, _time.LocalTimeZone).ToString("t", CultureInfo.CurrentCulture);
        (Status, StatusSeverity) = result switch
        {
            { Error: OdStocksError.KeyRejected or OdStocksError.NoKey } => (OdLocalization.Get("Stocks_StatusKeyRejected"), InfoBarSeverity.Error),
            { Error: not null } => (OdLocalization.Get("Stocks_StatusOffline"), InfoBarSeverity.Warning),
            { DayExhausted: true } => (OdLocalization.Format("Stocks_StatusDay", local(_limiter.NextDay)), InfoBarSeverity.Warning),
            { NextAttempt: { } next } => (OdLocalization.Format("Stocks_StatusMinute", local(next)), InfoBarSeverity.Informational),
            _ => ((string?)null, InfoBarSeverity.Informational),
        };
    }

    // A change of language: the rows, the status and the credits again, and the details, built anew from the cache.
    private void OnCultureChanged(object? sender, EventArgs e)
    {
        ShowRows();
        ShowCredits();
        if (_lastResult is { } result)
        {
            ShowResult(result);
        }

        if (SelectedRow is { } row)
        {
            OnSelectedRowChanged(row);
        }
    }

    private void ShowCredits()
    {
        Credits = OdLocalization.Format("Stocks_Credits", _limiter.LeftToday, _limiter.DayLimit);
        Plan = _settings.Plan is { Length: > 0 } plan ? OdLocalization.Format("Stocks_Plan", plan, _limiter.MinuteLimit, _limiter.DayLimit) : "";
    }

    private void SaveWatchlist() => Save(_settings with { Watchlist = [.. Rows.Select(row => row.Item)] });

    private void Save(OdStocksSettings settings)
    {
        _settings = settings;
        _save(settings);
    }
}

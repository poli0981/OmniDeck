using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Serilog;

namespace OmniDeck.Modules.Stocks;

/// <summary>
/// Twelve Data, with the user's own key (docs/spikes/07-providers.md). The key travels in an
/// <c>Authorization: apikey</c> header set on each request, never in the URL and never on the shared
/// client; logs name the endpoint and how many listings, never the key or a symbol, since the watchlist
/// is the user's own and logs end up in bug reports.
/// </summary>
public sealed class OdTwelveData(HttpClient http, ILogger log) : IOdQuoteProvider
{
    private static readonly Uri BaseUri = new("https://api.twelvedata.com/");

    private readonly ILogger _log = log.ForContext<OdTwelveData>();

    public async Task<OdQuoteBatch> QuotesAsync(IReadOnlyList<OdTicker> tickers, string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tickers);
        if (tickers.Count == 0)
        {
            return new OdQuoteBatch(new Dictionary<string, OdQuote>(StringComparer.Ordinal), new Dictionary<string, OdStocksError>(StringComparer.Ordinal));
        }

        var symbols = string.Join(',', tickers.Select(ticker => Uri.EscapeDataString(ticker.Query)));
        var (status, document) = await GetAsync("quote", "quote?symbol=" + symbols, tickers.Count, key, cancellationToken);
        using (document)
        {
            var root = document.RootElement;
            var quotes = new Dictionary<string, OdQuote>(StringComparer.Ordinal);
            var failed = new Dictionary<string, OdStocksError>(StringComparer.Ordinal);

            // Several symbols come back as an object keyed by symbol, one as a bare quote; an answer
            // keyed by none of them is the whole request's error.
            var keyed = root.ValueKind == JsonValueKind.Object && tickers.Any(ticker => root.TryGetProperty(ticker.Query, out _));
            if (!keyed)
            {
                if (OdTwelveDataJson.ErrorOf(root, status) is { } whole)
                {
                    ThrowUnlessListingError(whole);
                    return new OdQuoteBatch(quotes, tickers.ToDictionary(ticker => ticker.Key, _ => whole, StringComparer.Ordinal));
                }

                if (tickers.Count == 1)
                {
                    quotes[tickers[0].Key] = OdTwelveDataJson.Quote(root, tickers[0]);
                    return new OdQuoteBatch(quotes, failed);
                }
            }

            foreach (var ticker in tickers)
            {
                if (!root.TryGetProperty(ticker.Query, out var element) || element.ValueKind != JsonValueKind.Object)
                {
                    failed[ticker.Key] = OdStocksError.NotFound;
                }
                else if (OdTwelveDataJson.ErrorOf(element, 200) is { } error)
                {
                    ThrowUnlessListingError(error);
                    failed[ticker.Key] = error;
                }
                else
                {
                    quotes[ticker.Key] = OdTwelveDataJson.Quote(element, ticker);
                }
            }

            return new OdQuoteBatch(quotes, failed);
        }
    }

    public async Task<OdSeries> SeriesAsync(OdTicker ticker, OdSeriesRange range, string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        var (interval, size) = range == OdSeriesRange.Weekly ? ("1week", 261) : ("1day", 252);
        var path = $"time_series?symbol={Uri.EscapeDataString(ticker.Query)}&interval={interval}&outputsize={size}";
        var (status, document) = await GetAsync("time_series", path, 1, key, cancellationToken);
        using (document)
        {
            ThrowIfError(document.RootElement, status);
            return OdTwelveDataJson.Series(document.RootElement, ticker, range);
        }
    }

    public async Task<IReadOnlyList<OdListing>> SearchAsync(string text, string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var path = $"symbol_search?symbol={Uri.EscapeDataString(text.Trim())}&outputsize=30&show_plan=true";
        var (status, document) = await GetAsync("symbol_search", path, 0, key, cancellationToken);
        using (document)
        {
            ThrowIfError(document.RootElement, status);
            return OdTwelveDataJson.Listings(document.RootElement);
        }
    }

    public async Task<OdUsage> UsageAsync(string key, CancellationToken cancellationToken)
    {
        var (status, document) = await GetAsync("api_usage", "api_usage", 0, key, cancellationToken);
        using (document)
        {
            ThrowIfError(document.RootElement, status);
            return OdTwelveDataJson.Usage(document.RootElement);
        }
    }

    /// <summary>A plan limit or an unknown symbol concerns one listing; anything else concerns the whole request.</summary>
    private static void ThrowUnlessListingError(OdStocksError error)
    {
        if (error is not (OdStocksError.PlanRequired or OdStocksError.NotFound))
        {
            throw new OdStocksException(error);
        }
    }

    private static void ThrowIfError(JsonElement root, int status)
    {
        if (OdTwelveDataJson.ErrorOf(root, status) is { } error)
        {
            throw new OdStocksException(error);
        }
    }

    private async Task<(int Status, JsonDocument Document)> GetAsync(string endpoint, string pathAndQuery, int listings, string key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new OdStocksException(OdStocksError.NoKey);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseUri, pathAndQuery));
        request.Headers.Authorization = new AuthenticationHeaderValue("apikey", key);
        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            var status = (int)response.StatusCode;
            _log.Debug("Twelve Data {Endpoint} for {Listings} listings: HTTP {Status}", endpoint, listings, status);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            try
            {
                return (status, JsonDocument.Parse(body));
            }
            catch (JsonException ex)
            {
                throw new OdStocksException(status is >= 200 and < 300 ? OdStocksError.BadResponse : OdTwelveDataJson.Classify(status, null), ex);
            }
        }
        catch (HttpRequestException ex)
        {
            throw new OdStocksException(OdStocksError.Offline, ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // The client's own timeout, not the caller's cancellation.
            throw new OdStocksException(OdStocksError.Offline, ex);
        }
    }
}

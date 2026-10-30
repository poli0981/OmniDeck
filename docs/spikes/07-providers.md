# Spike 07 — Stock data providers

**Question:** which data sources can the stocks module rely on, and what do their responses look like?
**Decision:** GO with conditions. In v1 the only provider is Twelve Data, with the user's own key; Stooq is dropped. The free plan covers US listings (stocks, ETFs and the US listings of foreign companies). Home-market EU listings and indices need a paid Twelve Data plan.
**Measured:** 2026-10-30 on `spike/p0` at `6e8694a` (`spikes/ProviderProbe`). Issue [#8](https://github.com/poli0981/OmniDeck/issues/8).

## Environment

- **Client:** `ProviderProbe`, a .NET 10 console app using `HttpClient`. Requests are plain, carry a descriptive `User-Agent`, are never retried and solve no challenges.
- **Key:** read from the `OD_TWELVEDATA_KEY` environment variable and sent as `Authorization: apikey …`, never in a URL.
- **Account:** a free Twelve Data account; `api_usage` reports `plan_category: basic`.
- **Network:** a residential connection in Vietnam.

## Method

- **Stooq:** one GET each to the daily-history CSV endpoint and the quote CSV endpoint.
- **Twelve Data:** 12 calls paced 10 s apart, recording `api-credits-used` / `api-credits-left` from each response:
  - a quote and a 3-symbol batch quote;
  - time series for the 1M, 6M and 1Y ranges (daily bars) and 5Y (weekly bars);
  - symbol search;
  - quotes for three home-market EU listings and for an index;
  - `api_usage`.
- **Plan check:** one more `symbol_search` with `show_plan=true`.
- **Model check:** `ProviderProbe verify-models` deserializes the saved responses with the models below.
- **Raw responses:** kept locally under `%LocalAppData%\OmniDeck.Spikes\providers` and not committed, because the provider's terms cover redistribution of its data. Any free key reproduces them.

## Results

### Stooq: NO-GO

| Endpoint | Result |
|---|---|
| `/q/d/l/?s=aapl.us&i=d` (daily history CSV) | HTTP 200 `text/html`: a JavaScript proof-of-work page ("This site requires JavaScript to verify your browser"), no CSV |
| `/q/l/?s=aapl.us&f=sd2t2ohlcv&h&e=csv` (quote CSV) | HTTP 404 "The page you requested does not exist or has been moved" |

Community reports add that since March 2026 downloads also need a per-user API key, obtained through a captcha ([pandas-datareader #1012](https://github.com/pydata/pandas-datareader/issues/1012)). OmniDeck does not solve bot challenges, so the app cannot use Stooq at all.

### Twelve Data, free (Basic) plan

| Call | Credits | Result |
|---|---|---|
| `quote?symbol=AAPL` | 1 | OK |
| `quote?symbol=AAPL,MSFT,SPY` | 3 | OK: a JSON object keyed by symbol |
| `time_series`, `1day` × 22 / 126 / 252 (1M / 6M / 1Y) | 1 each | OK |
| `time_series`, `1week` × 261 (5Y, back to 2021-11-01) | 1 | OK |
| `symbol_search?symbol=SAP` | 0 | OK: 30 listings |
| `quote`: SAP on XETR, VOD on LSE, ASML on Euronext | 1 each | 404 "available starting with the Grow or Venture plan" |
| `quote?symbol=SPX` (S&P 500 index) | 1 | 404 "available starting with the Pro or Venture plan" |
| `api_usage` | 1 | OK: 8 credits per minute, 800 per day |

What this means for the client:

- **Credits:**
  - A quote costs 1 credit per symbol, so a batch of N costs N.
  - A time series costs 1 credit whatever its length.
  - Calls that fail still cost a credit.
  - `symbol_search` costs nothing.
- **Limits:** the per-minute quota is restored at the start of each minute, and going over it returns 429 ([API credits](https://support.twelvedata.com/en/articles/5194820-api-credits-limits)). A client-side limiter allowing ≤ 8 credits per rolling 60 s therefore never trips it.
- **Plan per listing:** `symbol_search` with `show_plan=true` returns `access: { global, plan }` for each listing. For SAP, the NYSE ADR needs Basic, XETR needs Grow and JSE needs Pro. The app can therefore show which listings a free key can use before spending a credit.
- **No key:** a request without a key gets HTTP 401 with a JSON error body.
- **Latency:** 280–630 ms per call.
- **Budget example:** a watchlist of 20 US symbols refreshed every 15 minutes through a US trading day costs 26 × 20 = 520 credits. That leaves 280 for charts. Because of the per-minute limit, each refresh spreads over three minutes.

### Response models (frozen for P3)

Numbers arrive as JSON strings and names are snake_case, so one set of options covers every call. `verify-models` parses all saved responses with these records:

```csharp
static readonly JsonSerializerOptions TwelveDataJson = new(JsonSerializerDefaults.Web)
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
};

// GET /quote. The batch form (symbol=A,B,C) returns Dictionary<string, TdQuote>.
internal sealed record TdQuote(
    string Symbol, string Name, string Exchange, string MicCode, string Currency, string Datetime, long Timestamp,
    long? LastQuoteAt, decimal Open, decimal High, decimal Low, decimal Close, long? Volume, decimal PreviousClose,
    decimal Change, decimal PercentChange, bool IsMarketOpen, TdFiftyTwoWeek? FiftyTwoWeek);
internal sealed record TdFiftyTwoWeek(decimal Low, decimal High, string Range);

// GET /time_series. Values are newest first; Datetime is a date for 1day/1week bars.
internal sealed record TdTimeSeries(TdSeriesMeta Meta, IReadOnlyList<TdBar> Values, string Status);
internal sealed record TdSeriesMeta(
    string Symbol, string Interval, string Currency, string ExchangeTimezone, string Exchange, string MicCode, string Type);
internal sealed record TdBar(string Datetime, decimal Open, decimal High, decimal Low, decimal Close, long? Volume);

// GET /symbol_search?show_plan=true (free).
internal sealed record TdSymbolSearch(IReadOnlyList<TdListing> Data, string Status);
internal sealed record TdListing(
    string Symbol, string InstrumentName, string Exchange, string MicCode, string ExchangeTimezone,
    string InstrumentType, string Country, string Currency, TdAccess? Access);
internal sealed record TdAccess(string Global, string Plan);

// GET /api_usage (1 credit), and any error (Code mirrors the HTTP status: 401, 404, 429).
internal sealed record TdApiUsage(string Timestamp, int CurrentUsage, int PlanLimit, int DailyUsage, int PlanDailyLimit, string PlanCategory);
internal sealed record TdError(int Code, string Message, string Status);
```

### Alternative considered: Alpha Vantage

This was desk research only; no account was created.

- **Limits:** a free key allows 25 requests per day and 5 per minute ([Macroption](https://www.macroption.com/alpha-vantage-api-limits/)).
- **Coverage:** the free key does reach home-market EU listings at end of day, such as `TSCO.LON` and `MBG.DEX` ([documentation](https://www.alphavantage.co/documentation/)).
- **Limitations:**
  - a free key returns only the latest 100 daily bars (the "full" output is premium);
  - each quote call covers a single symbol.

That is too little to drive a watchlist, and a second key doubles the setup. It stays on the post-v1 list as a possible add-on for EU end-of-day data.

## Consequences

- **Provider:** Twelve Data only, with the user's own key on any plan. The key is still stored with DPAPI.
  - The stocks module now needs setup once. Its first screen explains how to get a free key and where to paste it.
  - Stocks with zero configuration is gone.
- **Coverage:** with a free key, US stocks, ETFs and US-listed ADRs. EU companies are reachable through their US listings, and EU exposure through US-listed ETFs.
  - Grow unlocks home-market EU listings; Pro unlocks indices.
  - Search labels each listing with the plan it needs, compared with `api_usage.plan_category`.
  - The 404 plan error is shown as "not available on your Twelve Data plan", never as a failure.
- **Rate limiting:**
  - ≤ 8 credits per rolling 60 s, plus a daily budget of 800 that counts failed calls;
  - watchlist refreshes use batch quotes;
  - daily and weekly bars are cached until the next trading day.
- **Docs, at P0 exit:**
  - SPEC §3.5: providers and coverage;
  - SECURITY.md: the list of services the app calls;
  - ROADMAP: the P3 text and the risk register.
- **Legal docs, only after the maintainer approves:**
  - PRIVACY.md: the network list;
  - THIRD-PARTY-NOTICES.md: the Stooq row;
  - the Stooq mentions in DISCLAIMER.md and EULA.md.
- **Cut order unchanged:** Stocks is still the first thing to move to v1.1 if the schedule slips.

## Reproduce

On `spike/p0`, with `OD_TWELVEDATA_KEY` set to a free key:

```powershell
dotnet run --project spikes/ProviderProbe -- stooq
dotnet run --project spikes/ProviderProbe -- twelvedata
dotnet run --project spikes/ProviderProbe -- verify-models
```

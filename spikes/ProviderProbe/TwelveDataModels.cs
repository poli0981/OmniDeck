using System.Text.Json;
using System.Text.Json.Serialization;

// The Twelve Data response shapes frozen by spike #7 (docs/spikes/07-providers.md). Numbers arrive as
// JSON strings, so the options read numbers from strings; names are snake_case.
internal static class TwelveData
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };
}

/// <summary>GET /quote. The batch form (symbol=A,B,C) returns an object keyed by symbol.</summary>
internal sealed record TdQuote(
    string Symbol, string Name, string Exchange, string MicCode, string Currency, string Datetime, long Timestamp,
    long? LastQuoteAt, decimal Open, decimal High, decimal Low, decimal Close, long? Volume, decimal PreviousClose,
    decimal Change, decimal PercentChange, bool IsMarketOpen, TdFiftyTwoWeek? FiftyTwoWeek);

internal sealed record TdFiftyTwoWeek(decimal Low, decimal High, string Range);

/// <summary>GET /time_series. Values are newest first; Datetime is a date for 1day/1week bars.</summary>
internal sealed record TdTimeSeries(TdSeriesMeta Meta, IReadOnlyList<TdBar> Values, string Status);

internal sealed record TdSeriesMeta(
    string Symbol, string Interval, string Currency, string ExchangeTimezone, string Exchange, string MicCode, string Type);

internal sealed record TdBar(string Datetime, decimal Open, decimal High, decimal Low, decimal Close, long? Volume);

/// <summary>GET /symbol_search (free). With show_plan=true each listing carries the plan it needs.</summary>
internal sealed record TdSymbolSearch(IReadOnlyList<TdListing> Data, string Status);

internal sealed record TdListing(
    string Symbol, string InstrumentName, string Exchange, string MicCode, string ExchangeTimezone,
    string InstrumentType, string Country, string Currency, TdAccess? Access);

internal sealed record TdAccess(string Global, string Plan);

/// <summary>GET /api_usage (1 credit).</summary>
internal sealed record TdApiUsage(string Timestamp, int CurrentUsage, int PlanLimit, int DailyUsage, int PlanDailyLimit, string PlanCategory);

/// <summary>Any error; the HTTP status is mirrored in Code (401 no key, 404 symbol/plan, 429 credits).</summary>
internal sealed record TdError(int Code, string Message, string Status);

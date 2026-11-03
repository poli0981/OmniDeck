using System.Globalization;
using System.Text.Json;
using OmniDeck.Modules.Charts;

namespace OmniDeck.Modules.Stocks;

/// <summary>
/// Reads Twelve Data's answers (docs/spikes/07-providers.md): numbers come as strings, any figure may
/// be missing, unknown fields are ignored, and an error is <c>{code, message, status: "error"}</c>,
/// either as the whole answer or, in a batch of quotes, in place of one listing.
/// </summary>
internal static class OdTwelveDataJson
{
    /// <summary>The error an object stands for, or null when it is not one.</summary>
    public static OdStocksError? ErrorOf(JsonElement element, int httpStatus)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var isError = string.Equals(Text(element, "status"), "error", StringComparison.OrdinalIgnoreCase);
        if (!isError && httpStatus is >= 200 and < 300)
        {
            return null;
        }

        var code = element.TryGetProperty("code", out var codeValue) && codeValue.TryGetInt32(out var parsed) ? parsed : httpStatus;
        return Classify(code, Text(element, "message"));
    }

    /// <summary>What an error code and message mean for the screen.</summary>
    public static OdStocksError Classify(int code, string? message)
    {
        var text = message ?? "";
        return code switch
        {
            401 => OdStocksError.KeyRejected,
            403 => OdStocksError.PlanRequired,
            404 when text.Contains("available starting with", StringComparison.OrdinalIgnoreCase) => OdStocksError.PlanRequired,
            400 or 404 or 414 => OdStocksError.NotFound,
            429 when text.Contains("day", StringComparison.OrdinalIgnoreCase) => OdStocksError.DayLimit,
            429 => OdStocksError.MinuteLimit,
            >= 500 => OdStocksError.Offline,
            _ => OdStocksError.BadResponse,
        };
    }

    public static OdQuote Quote(JsonElement element, OdTicker ticker)
    {
        DateTimeOffset? asOf = (Integer(element, "last_quote_at") ?? Integer(element, "timestamp")) is { } seconds
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
        var year = element.TryGetProperty("fifty_two_week", out var range) && range.ValueKind == JsonValueKind.Object ? range : default;
        return new OdQuote(
            ticker,
            Text(element, "name"),
            Text(element, "exchange"),
            Text(element, "currency"),
            Number(element, "close"),
            Number(element, "open"),
            Number(element, "high"),
            Number(element, "low"),
            Number(element, "previous_close"),
            Number(element, "change"),
            Number(element, "percent_change"),
            Integer(element, "volume"),
            year.ValueKind == JsonValueKind.Object ? Number(year, "low") : null,
            year.ValueKind == JsonValueKind.Object ? Number(year, "high") : null,
            element.TryGetProperty("is_market_open", out var open) && open.ValueKind is JsonValueKind.True or JsonValueKind.False ? open.GetBoolean() : null,
            asOf);
    }

    /// <summary>A series' bars, oldest first; a bar without its four prices is left out.</summary>
    public static OdSeries Series(JsonElement root, OdTicker ticker, OdSeriesRange range)
    {
        var currency = root.TryGetProperty("meta", out var meta) && meta.ValueKind == JsonValueKind.Object ? Text(meta, "currency") : null;
        var bars = new List<OdOhlc>();
        if (root.TryGetProperty("values", out var values) && values.ValueKind == JsonValueKind.Array)
        {
            foreach (var value in values.EnumerateArray())
            {
                if (Bar(value) is { } bar)
                {
                    bars.Add(bar);
                }
            }
        }

        bars.Sort((a, b) => a.Time.CompareTo(b.Time));
        return new OdSeries(ticker, range, currency, bars);
    }

    public static IReadOnlyList<OdListing> Listings(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var listings = new List<OdListing>();
        foreach (var item in data.EnumerateArray())
        {
            if (Text(item, "symbol") is not { Length: > 0 } symbol)
            {
                continue;
            }

            var plan = item.TryGetProperty("access", out var access) && access.ValueKind == JsonValueKind.Object ? Text(access, "plan") : null;
            listings.Add(new OdListing(
                symbol,
                Text(item, "instrument_name") ?? symbol,
                Text(item, "exchange"),
                Text(item, "mic_code"),
                Text(item, "country"),
                Text(item, "currency"),
                Text(item, "instrument_type"),
                plan));
        }

        return listings;
    }

    public static OdUsage Usage(JsonElement root) => new(
        (int)(Integer(root, "current_usage") ?? 0),
        (int)(Integer(root, "plan_limit") ?? OdCreditLimiter.FreeMinuteLimit),
        (int)(Integer(root, "daily_usage") ?? 0),
        (int)(Integer(root, "plan_daily_limit") ?? OdCreditLimiter.FreeDayLimit),
        Text(root, "plan_category") ?? "basic");

    private static OdOhlc? Bar(JsonElement value)
    {
        if (Text(value, "datetime") is not { } stamp
            || !DateTime.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            || Number(value, "open") is not { } open
            || Number(value, "high") is not { } high
            || Number(value, "low") is not { } low
            || Number(value, "close") is not { } close)
        {
            return null;
        }

        return new OdOhlc(time, (double)open, (double)high, (double)low, (double)close, Integer(value, "volume"));
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static decimal? Number(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            JsonValueKind.String when decimal.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => number,
            _ => null,
        };
    }

    private static long? Integer(JsonElement element, string name) =>
        Number(element, name) is { } number && number >= long.MinValue && number <= long.MaxValue ? (long)Math.Round(number) : null;
}

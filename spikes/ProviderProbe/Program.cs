// Spike #7 (issue #8): probe stock data providers and keep the raw responses for freezing models.
//
//   dotnet run --project spikes/ProviderProbe -- stooq
//   dotnet run --project spikes/ProviderProbe -- twelvedata
//
// Output goes to %LocalAppData%\OmniDeck.Spikes\providers\. Rules (docs/spikes/README.md): single
// plain requests, no bot-challenge solving. The Twelve Data key comes from OD_TWELVEDATA_KEY, is
// sent in the Authorization header (never in a URL) and is never printed or written to disk.

using System.Diagnostics;
using System.Text;
using System.Text.Json;

var outDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OmniDeck.Spikes", "providers");
Directory.CreateDirectory(outDir);
var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("OmniDeck-Spike/0.1 (+https://github.com/poli0981/OmniDeck)");

var rows = new List<string>();

switch (args.FirstOrDefault())
{
    case "stooq":
        await ProbeAsync("stooq-history", "https://stooq.com/q/d/l/?s=aapl.us&i=d", apiKey: null);
        await ProbeAsync("stooq-quote", "https://stooq.com/q/l/?s=aapl.us&f=sd2t2ohlcv&h&e=csv", apiKey: null);
        break;

    case "twelvedata":
        var key = Environment.GetEnvironmentVariable("OD_TWELVEDATA_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            Console.Error.WriteLine("Set OD_TWELVEDATA_KEY (a free key from https://twelvedata.com/pricing) first.");
            return 2;
        }

        // Free plan: 8 credits per minute. Calls are paced 10 s apart so even the 3-symbol batch
        // (3 credits) never pushes a minute over the limit.
        (string Name, string Path)[] calls =
        [
            ("quote-aapl", "quote?symbol=AAPL"),
            ("quote-batch3", "quote?symbol=AAPL,MSFT,SPY"),
            ("ts-1m-daily", "time_series?symbol=AAPL&interval=1day&outputsize=22"),
            ("ts-6m-daily", "time_series?symbol=AAPL&interval=1day&outputsize=126"),
            ("ts-1y-daily", "time_series?symbol=AAPL&interval=1day&outputsize=252"),
            ("ts-5y-weekly", "time_series?symbol=AAPL&interval=1week&outputsize=261"),
            ("search-sap", "symbol_search?symbol=SAP"),
            ("quote-sap-xetr", "quote?symbol=SAP&exchange=XETR"),
            ("quote-vod-lse", "quote?symbol=VOD&exchange=LSE"),
            ("quote-asml-euronext", "quote?symbol=ASML&exchange=Euronext"),
            ("quote-spx-index", "quote?symbol=SPX"),
            ("api-usage", "api_usage"),
        ];
        for (var i = 0; i < calls.Length; i++)
        {
            if (i > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
            }

            await ProbeAsync($"td-{calls[i].Name}", $"https://api.twelvedata.com/{calls[i].Path}", key);
        }

        break;

    case "shapes":
        // P3 addendum to docs/spikes/07-providers.md: the answers OmniDeck's parser has not seen from a
        // real key. Prints structure only (HTTP status, shape, field names, error codes and messages,
        // which credit headers came back), never a figure or the key, and saves nothing.
        //   dotnet run --project spikes/ProviderProbe -- shapes [429]
        var shapesKey = Environment.GetEnvironmentVariable("OD_TWELVEDATA_KEY");
        if (string.IsNullOrWhiteSpace(shapesKey))
        {
            Console.Error.WriteLine("Set OD_TWELVEDATA_KEY first; the key is only sent to Twelve Data, never printed.");
            return 2;
        }

        // About 10 credits, paced 10 s apart to stay under 8 per minute.
        (string Name, string Path)[] shapes =
        [
            ("batch-with-unknown", "quote?symbol=AAPL,ZZZZQX"),
            ("batch-with-plan-gated", "quote?symbol=AAPL,SAP:XETR"),
            ("mic-code-form", "quote?symbol=SAP&mic_code=XETR"),
            ("single-unknown", "quote?symbol=ZZZZQX"),
            ("trailing-comma", "quote?symbol=AAPL,"),
            ("dotted-symbol", "quote?symbol=BRK.B"),
            ("series-unknown", "time_series?symbol=ZZZZQX&interval=1day&outputsize=5"),
        ];
        for (var i = 0; i < shapes.Length; i++)
        {
            if (i > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
            }

            await ShapeAsync(shapes[i].Name, $"https://api.twelvedata.com/{shapes[i].Path}", shapesKey);
        }

        if (args.Contains("429"))
        {
            // Nine more credits at once, to see what going over the minute's eight looks like.
            Console.WriteLine("Waiting a minute, then nine quotes at once...");
            await Task.Delay(TimeSpan.FromSeconds(61));
            for (var i = 0; i < 9; i++)
            {
                await ShapeAsync($"burst-{i + 1}", "https://api.twelvedata.com/quote?symbol=AAPL", shapesKey);
            }
        }

        return 0;
    case "verify-models":
        // Deserializes the newest saved Twelve Data responses with the frozen models.
        string Latest(string name) => Directory.GetFiles(outDir, $"*-td-{name}.json").Max()!;
        T Read<T>(string name) => JsonSerializer.Deserialize<T>(File.ReadAllText(Latest(name)), TwelveData.Json)!;
        var quote = Read<TdQuote>("quote-aapl");
        Console.WriteLine($"quote: {quote.Symbol} {quote.Close} {quote.Currency} ({quote.PercentChange:+0.00;-0.00}%), 52w {quote.FiftyTwoWeek?.Low}-{quote.FiftyTwoWeek?.High}, market open {quote.IsMarketOpen}");
        var batch = Read<Dictionary<string, TdQuote>>("quote-batch3");
        Console.WriteLine($"batch: {string.Join(", ", batch.Select(kv => $"{kv.Key} {kv.Value.Close}"))}");
        foreach (var range in new[] { "ts-1m-daily", "ts-6m-daily", "ts-1y-daily", "ts-5y-weekly" })
        {
            var series = Read<TdTimeSeries>(range);
            Console.WriteLine($"{range}: {series.Values.Count} bars {series.Meta.Interval}, {series.Values[^1].Datetime} -> {series.Values[0].Datetime}, last close {series.Values[0].Close}, tz {series.Meta.ExchangeTimezone}");
        }

        var search = Read<TdSymbolSearch>("search-sap-showplan");
        Console.WriteLine($"search: {string.Join("; ", search.Data.Take(4).Select(l => $"{l.Symbol}@{l.MicCode} {l.InstrumentType} needs {l.Access?.Plan}"))}");
        var usage = Read<TdApiUsage>("api-usage");
        Console.WriteLine($"usage: {usage.DailyUsage}/{usage.PlanDailyLimit} today, {usage.CurrentUsage}/{usage.PlanLimit} this minute, plan {usage.PlanCategory}");
        var error = Read<TdError>("quote-sap-xetr");
        Console.WriteLine($"error: {error.Code} {error.Message[..60]}...");
        return 0;

    default:
        Console.Error.WriteLine("usage: ProviderProbe stooq | twelvedata | shapes [429] | verify-models");
        return 1;
}

var summary = Path.Combine(outDir, $"{stamp}-{args[0]}-summary.md");
await File.WriteAllLinesAsync(summary,
[
    "| Call | HTTP | Content-Type | ms | Credits used/left | Verdict |",
    "|---|---|---|---|---|---|",
    .. rows,
]);
Console.WriteLine($"\nSummary: {summary}");
return 0;

async Task ProbeAsync(string name, string url, string? apiKey)
{
    using var request = new HttpRequestMessage(HttpMethod.Get, url);
    if (apiKey is not null)
    {
        request.Headers.TryAddWithoutValidation("Authorization", $"apikey {apiKey}");
    }

    var sw = Stopwatch.StartNew();
    using var response = await http.SendAsync(request);
    var body = await response.Content.ReadAsStringAsync();
    sw.Stop();

    var contentType = response.Content.Headers.ContentType?.ToString() ?? "-";
    var credits = Header(response, "api-credits-used") is { } used ? $"{used}/{Header(response, "api-credits-left")}" : "-";
    var verdict = Classify(body);
    var ext = contentType.Contains("json", StringComparison.OrdinalIgnoreCase) ? "json"
        : contentType.Contains("csv", StringComparison.OrdinalIgnoreCase) ? "csv" : "txt";
    await File.WriteAllTextAsync(Path.Combine(outDir, $"{stamp}-{name}.{ext}"), body, Encoding.UTF8);

    rows.Add($"| `{name}` | {(int)response.StatusCode} | {contentType} | {sw.ElapsedMilliseconds} | {credits} | {verdict} |");
    Console.WriteLine($"{name,-22} {(int)response.StatusCode} {sw.ElapsedMilliseconds,5} ms  {credits,-9} {verdict}");
}

async Task ShapeAsync(string name, string url, string apiKey)
{
    using var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.TryAddWithoutValidation("Authorization", $"apikey {apiKey}");
    using var response = await http.SendAsync(request);
    var body = await response.Content.ReadAsStringAsync();
    var credits = string.Join(", ", new[] { "api-credits-used", "api-credits-left" }.Where(response.Headers.Contains));
    Console.WriteLine($"{name,-24} HTTP {(int)response.StatusCode}  credit headers: {(credits.Length == 0 ? "none" : credits)}");
    Console.WriteLine("    " + Describe(body));
}

static string Describe(string body)
{
    try
    {
        using var document = JsonDocument.Parse(body);
        return Shape(document.RootElement);
    }
    catch (JsonException)
    {
        return "not JSON";
    }
}

static string Shape(JsonElement element)
{
    if (element.ValueKind != JsonValueKind.Object)
    {
        return element.ValueKind.ToString();
    }

    if (ErrorOf(element) is { } error)
    {
        return error;
    }

    static string Fields(JsonElement value) => string.Join(",", value.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
    if (element.TryGetProperty("symbol", out _))
    {
        return "flat quote; fields: " + Fields(element);
    }

    if (element.TryGetProperty("values", out _))
    {
        return "series; fields: " + Fields(element);
    }

    return "keyed by symbol: " + string.Join("; ", element.EnumerateObject().Select(p => $"{p.Name} => {(ErrorOf(p.Value) ?? (p.Value.ValueKind == JsonValueKind.Object ? "quote" : p.Value.ValueKind.ToString()))}"));
}

static string? ErrorOf(JsonElement element) =>
    element.ValueKind == JsonValueKind.Object && element.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String && status.GetString() == "error"
        ? $"error code={(element.TryGetProperty("code", out var code) ? code.ToString() : "?")} message=\"{(element.TryGetProperty("message", out var message) ? message.GetString() : "")}\""
        : null;

static string? Header(HttpResponseMessage response, string name) =>
    response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

static string Classify(string body)
{
    var head = body.Length > 600 ? body[..600] : body;
    if (head.StartsWith("Date,", StringComparison.Ordinal) || head.StartsWith("Symbol,", StringComparison.Ordinal))
    {
        return "CSV data";
    }

    if (head.Contains("verify your browser", StringComparison.OrdinalIgnoreCase))
    {
        return "browser check (JavaScript proof of work)";
    }

    if (head.Contains("apikey", StringComparison.OrdinalIgnoreCase) && !head.TrimStart().StartsWith('{'))
    {
        return "API key required";
    }

    if (head.Contains("Exceeded the daily hits limit", StringComparison.OrdinalIgnoreCase))
    {
        return "daily limit";
    }

    if (head.TrimStart().StartsWith('{') || head.TrimStart().StartsWith('['))
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("status", out var status) && status.GetString() == "error")
            {
                var code = doc.RootElement.TryGetProperty("code", out var c) ? c.ToString() : "?";
                var message = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : "";
                return $"error {code}: {Truncate(message, 90)}";
            }

            return "JSON data";
        }
        catch (JsonException)
        {
            return "malformed JSON";
        }
    }

    return $"other: {Truncate(head.ReplaceLineEndings(" "), 60)}";
}

static string Truncate(string? text, int max) =>
    text is null ? "" : text.Length <= max ? text.Replace("|", "\\|") : text[..max].Replace("|", "\\|") + "…";

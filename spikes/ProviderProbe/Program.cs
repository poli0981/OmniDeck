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

    default:
        Console.Error.WriteLine("usage: ProviderProbe stooq | twelvedata");
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

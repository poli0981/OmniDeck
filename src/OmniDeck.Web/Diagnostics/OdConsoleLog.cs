using System.Globalization;
using System.Text;
using System.Text.Json;

namespace OmniDeck.Web.Diagnostics;

/// <summary>
/// What a web app's page wrote to its console (docs/spikes/04-cdp-console.md), from the DevTools
/// protocol's <c>Runtime</c> and <c>Log</c> events: the last 200 entries of every kind and the last 50
/// errors. It lives with the module, so hibernating the app keeps it, and only in memory: a bug report
/// takes a scrubbed copy when the user asks for one.
/// </summary>
public sealed class OdConsoleLog
{
    public const int Capacity = 200;
    public const int ErrorCapacity = 50;
    public const string Error = "error";
    public const string Warning = "warning";

    private readonly Queue<OdConsoleEntry> _all = new();
    private readonly Queue<OdConsoleEntry> _errors = new();
    private readonly Lock _gate = new();

    /// <summary>The last <see cref="Capacity"/> entries, oldest first.</summary>
    public IReadOnlyList<OdConsoleEntry> All
    {
        get
        {
            lock (_gate)
            {
                return [.. _all];
            }
        }
    }

    /// <summary>The last <see cref="ErrorCapacity"/> errors, oldest first.</summary>
    public IReadOnlyList<OdConsoleEntry> Errors
    {
        get
        {
            lock (_gate)
            {
                return [.. _errors];
            }
        }
    }

    public void Add(OdConsoleEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            Keep(_all, entry, Capacity);
            if (entry.IsError)
            {
                Keep(_errors, entry, ErrorCapacity);
            }
        }
    }

    /// <summary>Adds a <c>Runtime.consoleAPICalled</c> event: <c>console.log</c> and its kin.</summary>
    public void AddConsoleCall(string json) => Add(Parse(json, root =>
    {
        var arguments = root.TryGetProperty("args", out var args) ? [.. args.EnumerateArray()] : new List<JsonElement>();
        return (Level(root.GetProperty("type").GetString()), Format(arguments));
    }));

    /// <summary>Adds a <c>Runtime.exceptionThrown</c> event: an uncaught exception.</summary>
    public void AddException(string json) => Add(Parse(json, root =>
    {
        var details = root.GetProperty("exceptionDetails");
        var message = details.TryGetProperty("exception", out var exception) && exception.TryGetProperty("description", out var description)
            ? description.GetString()
            : details.GetProperty("text").GetString();
        return (Error, message ?? "");
    }));

    /// <summary>Adds a <c>Log.entryAdded</c> event: the browser's own messages, such as a failed load.</summary>
    public void AddLogEntry(string json) => Add(Parse(json, root =>
    {
        var entry = root.GetProperty("entry");
        var text = entry.GetProperty("text").GetString() ?? "";
        return (Level(entry.GetProperty("level").GetString()), entry.TryGetProperty("url", out var url) ? $"{text} ({url.GetString()})" : text);
    }, root => root.GetProperty("entry")));

    /// <summary>
    /// Renders console arguments as the browser's console does: a first string's <c>%s</c>, <c>%d</c>,
    /// <c>%i</c>, <c>%f</c>, <c>%o</c> and <c>%O</c> take the next argument, <c>%c</c> takes one and drops
    /// it (it is CSS), and the arguments left over follow, separated by spaces.
    /// </summary>
    internal static string Format(IReadOnlyList<JsonElement> arguments)
    {
        if (arguments.Count == 0)
        {
            return "";
        }

        var next = 1;
        var parts = new List<string>();
        if (arguments[0].TryGetProperty("type", out var type) && string.Equals(type.GetString(), "string", StringComparison.Ordinal))
        {
            var text = Text(arguments[0]);
            var builder = new StringBuilder(text.Length);
            var i = 0;
            while (i < text.Length)
            {
                var spec = i + 1 < text.Length && text[i] == '%' ? text[i + 1] : '\0';
                if (spec == '\0' || !"sdifoOc%".Contains(spec, StringComparison.Ordinal))
                {
                    builder.Append(text[i]);
                    i++;
                    continue;
                }

                if (spec == '%')
                {
                    builder.Append('%');
                }
                else if (next < arguments.Count)
                {
                    builder.Append(spec == 'c' ? "" : Text(arguments[next]));
                    next++;
                }

                i += 2;
            }

            parts.Add(builder.ToString());
        }
        else
        {
            next = 0;
        }

        parts.AddRange(arguments.Skip(next).Select(Text));
        return string.Join(' ', parts);
    }

    private static OdConsoleEntry Parse(string json, Func<JsonElement, (string Level, string Message)> read, Func<JsonElement, JsonElement>? timeHolder = null)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var (level, message) = read(root);
            var holder = timeHolder?.Invoke(root) ?? root;
            var time = holder.TryGetProperty("timestamp", out var stamp) && stamp.TryGetDouble(out var milliseconds)
                ? DateTimeOffset.FromUnixTimeMilliseconds((long)milliseconds)
                : DateTimeOffset.UtcNow;
            return new OdConsoleEntry(time, level, message);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return new OdConsoleEntry(DateTimeOffset.UtcNow, Warning, "(a console message OmniDeck could not read)");
        }
    }

    private static string Level(string? type) => type switch
    {
        "error" or "assert" => Error,
        "warning" or "warn" => Warning,
        "info" => "info",
        "debug" or "verbose" => "debug",
        _ => "log",
    };

    // A primitive's value as written, otherwise how DevTools describes the object ("Object", "Array(3)").
    private static string Text(JsonElement argument)
    {
        if (argument.TryGetProperty("value", out var value))
        {
            return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.GetRawText();
        }

        if (argument.TryGetProperty("unserializableValue", out var special))
        {
            return special.GetString() ?? "";
        }

        if (argument.TryGetProperty("description", out var description))
        {
            return description.GetString() ?? "";
        }

        return argument.TryGetProperty("type", out var type) ? type.GetString() ?? "" : "";
    }

    private static void Keep(Queue<OdConsoleEntry> queue, OdConsoleEntry entry, int capacity)
    {
        queue.Enqueue(entry);
        while (queue.Count > capacity)
        {
            queue.Dequeue();
        }
    }

    /// <summary>The console as plain text, one line per entry, for a bug report.</summary>
    public static string Write(IEnumerable<OdConsoleEntry> entries) => string.Join(
        Environment.NewLine,
        entries.Select(entry => string.Create(CultureInfo.InvariantCulture, $"{entry.Time:HH:mm:ss.fff} [{entry.Level}] {entry.Message}")));
}

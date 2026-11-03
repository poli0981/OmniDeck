using System.Globalization;
using System.Text.Json;
using OmniDeck.Core.Storage;

namespace OmniDeck.Modules.Stocks;

/// <summary>
/// Keeps Twelve Data spending inside the key's plan (docs/SPEC.md §3.5): at most
/// <see cref="MinuteLimit"/> credits in any 60 seconds, and <see cref="DayLimit"/> per UTC day.
/// Failed calls cost credits too, so every call is counted, and nothing is ever given back. After a
/// 429 nothing goes out until the next minute, or the next UTC day when the day is spent. The state
/// survives restarts in <c>app_meta</c>, so quitting cannot reset the day's count.
/// </summary>
public sealed class OdCreditLimiter
{
    /// <summary>The free plan's credits per minute.</summary>
    public const int FreeMinuteLimit = 8;

    /// <summary>The free plan's credits per day.</summary>
    public const int FreeDayLimit = 800;

    private const string MetaKey = "stocks.credits";
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly IOdAppMeta _meta;
    private readonly TimeProvider _time;
    private OdCreditState _state;

    public OdCreditLimiter(IOdAppMeta meta, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(meta);
        _meta = meta;
        _time = time;
        _state = Load(meta.Get(MetaKey));
    }

    public int MinuteLimit { get; private set; } = FreeMinuteLimit;

    public int DayLimit { get; private set; } = FreeDayLimit;

    /// <summary>Credits spent today (UTC), failed calls included.</summary>
    public int UsedToday => Current().Used;

    public int LeftToday => Math.Max(0, DayLimit - UsedToday);

    /// <summary>When today's credits come back: the next midnight UTC.</summary>
    public DateTimeOffset NextDay => new DateTimeOffset(_time.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero).AddDays(1);

    /// <summary>Takes the limits of the key's plan, from <c>api_usage</c>.</summary>
    public void SetLimits(int perMinute, int perDay)
    {
        MinuteLimit = Math.Max(1, perMinute);
        DayLimit = Math.Max(1, perDay);
    }

    /// <summary>
    /// How long to wait before spending <paramref name="credits"/>: zero when it can go now, or null when
    /// today's credits cannot cover it. A batch is at most a minute's credits.
    /// </summary>
    public TimeSpan? WaitFor(int credits)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(credits, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(credits, MinuteLimit);
        var state = Current();
        var now = _time.GetUtcNow();
        if (state.Used + credits > DayLimit)
        {
            return null;
        }

        if (state.BlockedUntil is { } blocked && blocked > now)
        {
            return blocked - now;
        }

        var recent = state.Recent.Where(spend => spend.At > now - Window).OrderBy(spend => spend.At).ToList();
        var inWindow = recent.Sum(spend => spend.Credits);
        if (inWindow + credits <= MinuteLimit)
        {
            return TimeSpan.Zero;
        }

        // The oldest spends leave the window first: wait until enough of them have.
        foreach (var spend in recent)
        {
            inWindow -= spend.Credits;
            if (inWindow + credits <= MinuteLimit)
            {
                return spend.At + Window - now;
            }
        }

        return TimeSpan.Zero;
    }

    /// <summary>Counts <paramref name="credits"/> spent now, whether the call worked or not.</summary>
    public void Spend(int credits)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(credits, 1);
        var state = Current();
        var now = _time.GetUtcNow();
        Save(state with
        {
            Used = state.Used + credits,
            Recent = [.. state.Recent.Where(spend => spend.At > now - Window), new OdCreditSpend(now, credits)],
        });
    }

    /// <summary>Twelve Data said 429: nothing goes out until the next minute, or the next UTC day when <paramref name="day"/>.</summary>
    public void Block(bool day)
    {
        var now = _time.GetUtcNow();
        var state = Current();
        var until = day ? NextDay : new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero).Add(Window);
        Save(state with { BlockedUntil = until, Used = day ? Math.Max(state.Used, DayLimit) : state.Used });
    }

    /// <summary>Takes Twelve Data's own count of today's credits, which also includes what the key spent elsewhere.</summary>
    public void Sync(int usedToday) => Save(Current() with { Used = Math.Max(0, usedToday) });

    private static OdCreditState Load(string? json)
    {
        if (json is not null)
        {
            try
            {
                if (JsonSerializer.Deserialize(json, OdModulesJsonContext.Default.OdCreditState) is { } state)
                {
                    return state;
                }
            }
            catch (JsonException)
            {
                // A damaged count starts again from zero; Twelve Data's own limit still holds.
            }
        }

        return new OdCreditState("", 0, [], null);
    }

    /// <summary>The state, started afresh on a new UTC day.</summary>
    private OdCreditState Current()
    {
        var today = _time.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (!string.Equals(_state.Day, today, StringComparison.Ordinal))
        {
            _state = _state with { Day = today, Used = 0 };
        }

        return _state;
    }

    private void Save(OdCreditState state)
    {
        _state = state;
        _meta.Set(MetaKey, JsonSerializer.Serialize(state, OdModulesJsonContext.Default.OdCreditState));
    }
}

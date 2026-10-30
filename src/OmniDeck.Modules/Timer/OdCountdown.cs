namespace OmniDeck.Modules.Timer;

/// <summary>One countdown. It keeps absolute times, so it loses nothing while no view shows it.</summary>
public sealed class OdCountdown
{
    internal OdCountdown(string name, TimeSpan duration)
    {
        Name = name;
        Duration = duration;
        Remaining = duration;
    }

    public string Name { get; }

    public TimeSpan Duration { get; }

    /// <summary>When it ends, while it runs.</summary>
    public DateTimeOffset? EndsAt { get; internal set; }

    /// <summary>What is left while it is paused.</summary>
    public TimeSpan Remaining { get; internal set; }

    public bool IsRunning => EndsAt is not null;

    /// <summary>It ran out; starting it again restarts the full duration.</summary>
    public bool IsFinished { get; internal set; }

    public TimeSpan RemainingAt(DateTimeOffset now)
    {
        if (EndsAt is not { } end)
        {
            return Remaining;
        }

        return end > now ? end - now : TimeSpan.Zero;
    }
}

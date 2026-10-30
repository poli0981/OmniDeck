namespace OmniDeck.Core.Settings;

/// <summary>The RAM governor's settings (docs/RAM-GOVERNOR.md §2), edited in Settings → Memory.</summary>
public sealed record OdMemorySettings
{
    /// <summary>The longest wait a threshold may hold: one week.</summary>
    public const int MaxMinutes = 7 * 24 * 60;

    /// <summary>Minutes a hidden module stays live before it is suspended.</summary>
    public int IdleAfterMinutes { get; init; } = 5;

    /// <summary>Minutes a hidden module waits before it is hibernated; never less than <see cref="IdleAfterMinutes"/>.</summary>
    public int HibernateAfterMinutes { get; init; } = 30;

    /// <summary>The soft cap in MiB; 0 picks min(4.5 GiB, 35 % of RAM).</summary>
    public int SoftCapMegabytes { get; init; }

    /// <summary>The hard cap in MiB; 0 picks min(6 GiB, 50 % of RAM). It never ends up below the soft cap.</summary>
    public int HardCapMegabytes { get; init; }

    /// <summary>
    /// Modules the user keeps running from the task-manager panel: the governor never demotes them.
    /// Like every list in the settings, it is compared by reference: change it by assigning a new list.
    /// </summary>
    public IReadOnlyList<string> KeepRunning { get; init; } = [];

    /// <summary>Repairs values a hand-edited or older file may carry.</summary>
    public OdMemorySettings Normalized()
    {
        var idle = Math.Clamp(IdleAfterMinutes, 1, MaxMinutes);
        var soft = Math.Max(SoftCapMegabytes, 0);
        var hard = Math.Max(HardCapMegabytes, 0);
        return this with
        {
            IdleAfterMinutes = idle,
            HibernateAfterMinutes = Math.Clamp(HibernateAfterMinutes, idle, MaxMinutes),
            SoftCapMegabytes = soft,
            HardCapMegabytes = soft > 0 && hard > 0 ? Math.Max(hard, soft) : hard,
            KeepRunning = OdSettings.DistinctIds(KeepRunning),
        };
    }
}

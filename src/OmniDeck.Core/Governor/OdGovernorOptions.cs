using OmniDeck.Core.Settings;

namespace OmniDeck.Core.Governor;

/// <summary>The thresholds one governor pass works with (docs/RAM-GOVERNOR.md §2).</summary>
public sealed record OdGovernorOptions(TimeSpan IdleAfter, TimeSpan HibernateAfter, long SoftCapBytes, long HardCapBytes, TimeSpan PollInterval)
{
    public const long MiB = 1L << 20;
    public const long GiB = 1L << 30;

    /// <summary>How often the governor samples and enforces.</summary>
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(10);

    /// <summary>The options <paramref name="settings"/> describe on a machine with <paramref name="physicalBytes"/> of RAM.</summary>
    public static OdGovernorOptions From(OdMemorySettings settings, long physicalBytes)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var soft = settings.SoftCapMegabytes > 0 ? settings.SoftCapMegabytes * MiB : Math.Min(45 * GiB / 10, physicalBytes / 100 * 35);
        var hard = settings.HardCapMegabytes > 0 ? settings.HardCapMegabytes * MiB : Math.Min(6 * GiB, physicalBytes / 2);
        return new OdGovernorOptions(
            TimeSpan.FromMinutes(settings.IdleAfterMinutes),
            TimeSpan.FromMinutes(settings.HibernateAfterMinutes),
            soft,
            Math.Max(hard, soft),
            DefaultPollInterval);
    }
}

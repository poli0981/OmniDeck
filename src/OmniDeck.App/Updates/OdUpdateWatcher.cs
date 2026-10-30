using OmniDeck.Core.Localization;
using OmniDeck.Core.Modules;
using Serilog;

namespace OmniDeck.App.Updates;

/// <summary>
/// The background update check (docs/ARCHITECTURE.md §7): a few minutes after start and then once a day,
/// an installed copy asks GitHub for a newer release and says so once per version in a notification. It
/// never downloads or restarts; Settings → About does that when the user asks.
/// </summary>
public sealed class OdUpdateWatcher(IOdUpdateService updates, IOdNotifier notifier, TimeProvider time, ILogger log) : IDisposable
{
    /// <summary>Leaves startup alone before the first check.</summary>
    public static readonly TimeSpan FirstCheckAfter = TimeSpan.FromMinutes(3);

    public static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    private readonly ILogger _log = log.ForContext<OdUpdateWatcher>();
    private ITimer? _timer;
    private string? _announced;

    /// <summary>Starts checking on the calling thread, which must be the UI thread. A copy that is not installed has nothing to check.</summary>
    public void Start()
    {
        if (!updates.IsInstalled)
        {
            return;
        }

        var context = SynchronizationContext.Current
            ?? throw new InvalidOperationException("Start the update watcher from the UI thread.");
        _timer?.Dispose();
        _timer = time.CreateTimer(_ => context.Post(_ => Poll(), null), null, FirstCheckAfter, Interval);
    }

    /// <summary>One check: a version not announced before gets one notification. Failures wait for the next day.</summary>
    public async Task CheckAsync()
    {
        try
        {
            var version = await updates.CheckAsync(CancellationToken.None);
            if (version is not null && !string.Equals(version, _announced, StringComparison.Ordinal))
            {
                _announced = version;
                notifier.Notify(OdLocalization.Format("Update_AvailableTitle", version), OdLocalization.Get("Update_AvailableMessage"));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.Information(ex, "Background update check failed");
        }
    }

    public void Dispose() => _timer?.Dispose();

    private void Poll() => _ = CheckAsync();
}

using OmniDeck.Core;
using OmniDeck.Core.Settings;
using Velopack;
using Velopack.Sources;

namespace OmniDeck.App.Updates;

/// <summary>
/// <see cref="IOdUpdateService"/> over Velopack and this repository's GitHub releases. Each check asks
/// the channel in use (<see cref="OdUpdateChannel"/>): with pre-releases while the user wants them or a
/// pre-release build runs, stable releases only otherwise. Per-user install: no UAC.
/// </summary>
public sealed class OdVelopackUpdateService(IOdSettingsService settings) : IOdUpdateService
{
    private const string Repository = "https://github.com/poli0981/OmniDeck";

    private static readonly string Running = OdVersionInfo.FromAssembly(typeof(OdVelopackUpdateService).Assembly).Version;

    private readonly Lazy<UpdateManager> _installed = new(() => Manager(prereleases: true));

    // The manager that found the pending update downloads and applies it, whatever the channel is by then.
    private (UpdateManager Manager, UpdateInfo Update)? _pending;

    public bool IsInstalled => _installed.Value.IsInstalled;

    public async Task<string?> CheckAsync(CancellationToken cancellationToken)
    {
        var manager = Manager(OdUpdateChannel.IncludesPrereleases(settings.Current.Prereleases, Running));

        // CheckForUpdatesAsync takes no token; waiting on it through Task.Run makes the wait cancellable.
        var update = await Task.Run(() => manager.CheckForUpdatesAsync(), cancellationToken);
        _pending = update is null ? null : (manager, update);
        return update?.TargetFullRelease.Version.ToString();
    }

    public Task DownloadAsync(IProgress<int> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var (manager, update) = _pending ?? throw new InvalidOperationException("Check for updates first.");
        return manager.DownloadUpdatesAsync(update, progress.Report, cancelToken: cancellationToken);
    }

    public void ApplyAndRestart()
    {
        var (manager, update) = _pending ?? throw new InvalidOperationException("Check for updates first.");
        manager.ApplyUpdatesAndRestart(update.TargetFullRelease);
    }

    private static UpdateManager Manager(bool prereleases) => new(new GithubSource(Repository, accessToken: null, prerelease: prereleases));
}

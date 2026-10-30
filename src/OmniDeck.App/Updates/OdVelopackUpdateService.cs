using Velopack;
using Velopack.Sources;

namespace OmniDeck.App.Updates;

/// <summary>
/// <see cref="IOdUpdateService"/> over Velopack and this repository's GitHub releases. Every version
/// below 1.0.0 is a pre-release, so pre-releases are included. Per-user install: no UAC.
/// </summary>
public sealed class OdVelopackUpdateService : IOdUpdateService
{
    private const string Repository = "https://github.com/poli0981/OmniDeck";

    private readonly Lazy<UpdateManager> _manager = new(() => new UpdateManager(new GithubSource(Repository, accessToken: null, prerelease: true)));
    private UpdateInfo? _pending;

    public bool IsInstalled => _manager.Value.IsInstalled;

    public async Task<string?> CheckAsync(CancellationToken cancellationToken)
    {
        // CheckForUpdatesAsync takes no token; waiting on it through Task.Run makes the wait cancellable.
        _pending = await Task.Run(() => _manager.Value.CheckForUpdatesAsync(), cancellationToken);
        return _pending?.TargetFullRelease.Version.ToString();
    }

    public Task DownloadAsync(IProgress<int> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var pending = _pending ?? throw new InvalidOperationException("Check for updates first.");
        return _manager.Value.DownloadUpdatesAsync(pending, progress.Report, cancelToken: cancellationToken);
    }

    public void ApplyAndRestart()
    {
        var pending = _pending ?? throw new InvalidOperationException("Check for updates first.");
        _manager.Value.ApplyUpdatesAndRestart(pending.TargetFullRelease);
    }
}

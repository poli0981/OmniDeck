using OmniDeck.Core.Storage;
using Serilog;

namespace OmniDeck.Core.Settings;

/// <summary>
/// <see cref="IOdSettingsService"/> over <c>settings.json</c> in the data root. A missing file means
/// defaults; a corrupt one is set aside (see <see cref="OdJsonFile"/>) and defaults take over.
/// </summary>
public sealed class OdSettingsService : IOdSettingsService
{
    private readonly string _path;
    private readonly Lock _gate = new();

    public OdSettingsService(OdDataRoot dataRoot, ILogger log)
    {
        ArgumentNullException.ThrowIfNull(dataRoot);
        ArgumentNullException.ThrowIfNull(log);
        _path = dataRoot.SettingsFile;
        var loaded = OdJsonFile.Read(_path, OdSettingsJsonContext.Default.OdSettings, out var status);
        if (status == OdJsonReadStatus.Quarantined)
        {
            log.ForContext<OdSettingsService>().Warning("settings.json could not be read; it was set aside and defaults are in use");
        }

        Current = (loaded ?? new OdSettings()).Normalized();
    }

    public event EventHandler<OdSettingsChangedEventArgs>? Changed;

    public OdSettings Current { get; private set; }

    public void Update(Func<OdSettings, OdSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        OdSettings next;
        lock (_gate)
        {
            next = change(Current).Normalized();
            if (next == Current)
            {
                return;
            }

            OdJsonFile.Write(_path, next, OdSettingsJsonContext.Default.OdSettings);
            Current = next;
        }

        Changed?.Invoke(this, new OdSettingsChangedEventArgs(next));
    }
}

using System.Globalization;
using OmniDeck.Core.Storage;
using Serilog;

namespace OmniDeck.Web.Diagnostics;

/// <summary>
/// Whether web apps come back signed in after a restart (docs/spikes/02-google-sign-in.md: one early
/// session lost every cookie). It keeps the number of cookies each profile holds for its app's start
/// page, never a value or a name, when the app closes, and compares it at the first load after the
/// next start: a profile that had cookies and comes back with none is logged as a warning.
/// </summary>
public sealed class OdSignInHealth(IOdAppMeta meta, ILogger log)
{
    private readonly ILogger _log = log.ForContext<OdSignInHealth>();
    private readonly HashSet<string> _checked = new(StringComparer.Ordinal);

    /// <summary>Whether the first load of <paramref name="profile"/> this session is still to be checked.</summary>
    public bool NeedsCheck(string profile) => !_checked.Contains(profile);

    /// <summary>Compares the first load's count with the one kept at the last close.</summary>
    public void AtFirstLoad(string profile, int count)
    {
        if (!_checked.Add(profile))
        {
            return;
        }

        if (!int.TryParse(meta.Get(Key(profile)), NumberStyles.None, CultureInfo.InvariantCulture, out var before))
        {
            _log.Information("Web profile {Profile} loaded with {Count} cookies; no earlier close kept a count", profile, count);
        }
        else if (before > 0 && count == 0)
        {
            _log.Warning("Web profile {Profile} came back with no cookies (it had {Before} at the last close): sign-ins were lost", profile, before);
        }
        else
        {
            _log.Information("Web profile {Profile} loaded with {Count} cookies ({Before} at the last close)", profile, count, before);
        }
    }

    /// <summary>Keeps the count as the app closes or hibernates.</summary>
    public void AtClose(string profile, int count)
    {
        meta.Set(Key(profile), count.ToString(CultureInfo.InvariantCulture));
        _log.Information("Web profile {Profile} closed with {Count} cookies", profile, count);
    }

    /// <summary>The count kept at the profile's last close; null when none was kept.</summary>
    public int? Kept(string profile) =>
        int.TryParse(meta.Get(Key(profile)), NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? count : null;

    /// <summary>After the profile's data was cleared on purpose: coming back with no cookies is expected.</summary>
    public void Forget(string profile) => meta.Set(Key(profile), "0");

    private static string Key(string profile) => "web.cookies." + profile;
}

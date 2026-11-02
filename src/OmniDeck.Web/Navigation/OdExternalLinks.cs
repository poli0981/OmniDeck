using System.Diagnostics;

namespace OmniDeck.Web.Navigation;

/// <summary>
/// Opens one web app's outside links in the default browser, and holds back a link the page keeps
/// sending out: Google once retried a blocked sign-in hop 15 times in 40 seconds
/// (docs/spikes/02-google-sign-in.md), which would have opened 15 browser tabs.
/// </summary>
public sealed class OdExternalLinks(TimeProvider time, Action<Uri> open)
{
    /// <summary>How long a link's openings are remembered.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(30);

    /// <summary>How often the same link opens within <see cref="Window"/> before it is held back.</summary>
    public const int MostPerWindow = 3;

    private readonly Dictionary<string, List<DateTimeOffset>> _opened = new(StringComparer.Ordinal);

    /// <summary>Raised once per window when a repeating link is held back.</summary>
    public event EventHandler<OdLinkEventArgs>? HeldBack;

    /// <summary>Opens <paramref name="target"/> in the default browser; false when it was held back.</summary>
    public bool Open(Uri target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var key = target.GetLeftPart(UriPartial.Path);
        var now = time.GetUtcNow();
        if (!_opened.TryGetValue(key, out var opened))
        {
            opened = [];
            _opened[key] = opened;
        }

        opened.RemoveAll(at => now - at >= Window);
        if (opened.Count >= MostPerWindow)
        {
            // One extra mark says the user was told, so the notice shows once per window.
            if (opened.Count == MostPerWindow)
            {
                opened.Add(now);
                HeldBack?.Invoke(this, new OdLinkEventArgs(target));
            }

            return false;
        }

        opened.Add(now);
        open(target);
        return true;
    }

    /// <summary>Opens a link with Windows' default handler: the browser, or the mail app for <c>mailto:</c>.</summary>
    public static void ShellOpen(Uri target)
    {
        ArgumentNullException.ThrowIfNull(target);
        Process.Start(new ProcessStartInfo(target.AbsoluteUri) { UseShellExecute = true })?.Dispose();
    }
}

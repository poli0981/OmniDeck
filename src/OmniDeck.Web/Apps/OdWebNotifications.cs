using OmniDeck.Core.Localization;
using OmniDeck.Core.Modules;

namespace OmniDeck.Web.Apps;

/// <summary>A page's notification as OmniDeck shows it: a Windows notification under the app's name.</summary>
internal static class OdWebNotifications
{
    // What a notification-area balloon holds: NOTIFYICONDATA's szInfoTitle and szInfo, less the terminator.
    internal const int MostTitle = 63;
    internal const int MostMessage = 255;

    /// <summary>
    /// The app's name and the page's title on the first line, the body below. Without a body the title
    /// moves down, because Windows drops a notification that has no text.
    /// </summary>
    internal static OdNotification From(string app, string? title, string? body, Action clicked)
    {
        var heading = string.Join(' ', (title ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var text = (body ?? "").Trim();
        if (text.Length == 0)
        {
            text = heading.Length > 0 ? heading : OdLocalization.Get("Web_NotificationFallback");
            heading = "";
        }

        var first = heading.Length > 0 ? OdLocalization.Format("Web_NotificationTitle", app, heading) : app;
        return new OdNotification(Cut(first, MostTitle), Cut(text, MostMessage), Clicked: clicked);
    }

    private static string Cut(string text, int most)
    {
        if (text.Length <= most)
        {
            return text;
        }

        // Room for the ellipsis, without splitting a surrogate pair.
        var end = most - 1;
        if (char.IsHighSurrogate(text[end - 1]))
        {
            end--;
        }

        return string.Concat(text.AsSpan(0, end), "\u2026");
    }
}

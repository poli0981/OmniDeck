using Microsoft.Web.WebView2.Core;
using OmniDeck.Core.Localization;

namespace OmniDeck.Web.Apps;

/// <summary>
/// How a web app's permission requests are answered (docs/ARCHITECTURE.md §3). Notifications follow
/// the app's notifications switch. The kinds that reach into the user's devices or data are asked about
/// once per app, and the answer is kept. Autoplay and persistent storage are left to the browser, and
/// any other kind is refused.
/// </summary>
public static class OdWebPermissions
{
    /// <summary>The kinds OmniDeck asks the user about, once per app.</summary>
    public static IReadOnlyList<CoreWebView2PermissionKind> Asked { get; } =
    [
        CoreWebView2PermissionKind.Microphone,
        CoreWebView2PermissionKind.Camera,
        CoreWebView2PermissionKind.Geolocation,
        CoreWebView2PermissionKind.ClipboardRead,
        CoreWebView2PermissionKind.OtherSensors,
        CoreWebView2PermissionKind.MultipleAutomaticDownloads,
        CoreWebView2PermissionKind.FileReadWrite,
        CoreWebView2PermissionKind.LocalFonts,
        CoreWebView2PermissionKind.MidiSystemExclusiveMessages,
        CoreWebView2PermissionKind.WindowManagement,
    ];

    /// <summary>The answer to a request for <paramref name="kind"/>, or null when the user has to be asked.</summary>
    /// <param name="kind">What the page asks for.</param>
    /// <param name="notificationsOn">The app's notifications switch.</param>
    /// <param name="remembered">The user's earlier answer for this kind, if any.</param>
    public static CoreWebView2PermissionState? Decide(CoreWebView2PermissionKind kind, bool notificationsOn, bool? remembered) => kind switch
    {
        CoreWebView2PermissionKind.Notifications => notificationsOn ? CoreWebView2PermissionState.Allow : CoreWebView2PermissionState.Deny,
        CoreWebView2PermissionKind.Autoplay or CoreWebView2PermissionKind.PersistentStorage => CoreWebView2PermissionState.Default,
        _ when !Asked.Contains(kind) => CoreWebView2PermissionState.Deny,
        _ => remembered switch
        {
            true => CoreWebView2PermissionState.Allow,
            false => CoreWebView2PermissionState.Deny,
            null => null,
        },
    };

    /// <summary>The question shown for <paramref name="kind"/> in the app <paramref name="app"/>.</summary>
    public static string Question(CoreWebView2PermissionKind kind, string app) => OdLocalization.Format(QuestionKey(kind), app);

    /// <summary>A stored kind's name, as Settings lists it; a kind this build does not ask about shows as stored.</summary>
    public static string Name(string kind) =>
        Enum.TryParse<CoreWebView2PermissionKind>(kind, out var known) && Asked.Contains(known) ? OdLocalization.Get(NameKey(known)) : kind;

    internal static string QuestionKey(CoreWebView2PermissionKind kind) => "Web_Ask" + kind;

    internal static string NameKey(CoreWebView2PermissionKind kind) => "Web_Kind" + kind;
}

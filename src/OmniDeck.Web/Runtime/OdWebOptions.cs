namespace OmniDeck.Web.Runtime;

/// <summary>How the web runner starts WebView2.</summary>
/// <param name="ChromiumLogging">
/// Chromium's own logging (<c>--enable-logging --v=0</c>), for chasing a lost sign-in while dogfooding
/// (docs/spikes/02-google-sign-in.md); turned on by starting OmniDeck with <see cref="LoggingSwitch"/>.
/// </param>
public sealed record OdWebOptions(bool ChromiumLogging = false)
{
    /// <summary>The command-line switch that turns on <see cref="ChromiumLogging"/>.</summary>
    public const string LoggingSwitch = "--wv2-logging";
}

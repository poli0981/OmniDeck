using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace OmniDeck.Core.Net;

/// <summary>
/// Builds the one HTTP client OmniDeck's own features share (docs/ARCHITECTURE.md §2): weather and the
/// catalog refresh. Web apps never go through it; WebView2 makes their connections.
/// </summary>
public static class OdHttp
{
    /// <summary>How long a request may take before it fails.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public static HttpClient Create(OdVersionInfo version)
    {
        ArgumentNullException.ThrowIfNull(version);
        var handler = new SocketsHttpHandler
        {
            // The client lives as long as the app: renewing pooled connections picks up DNS changes.
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = false,
        };
        var client = new HttpClient(handler) { Timeout = Timeout };

        // A descriptive User-Agent, as the providers ask of scripted clients (docs/spikes/07-providers.md).
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OmniDeck", version.Version));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(+https://github.com/poli0981/OmniDeck)"));
        return client;
    }
}

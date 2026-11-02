namespace OmniDeck.Web.Runtime;

/// <summary>A main frame a WebView2 process hosts part of.</summary>
/// <param name="Id">WebView2's frame id.</param>
/// <param name="Host">The frame's host; empty when it has none.</param>
internal sealed record OdWebFrame(uint Id, string Host);

namespace OmniDeck.Web.Runtime;

/// <summary>The page one open browser shows, as the memory figures match processes to apps.</summary>
/// <param name="ModuleId">The web app the browser belongs to.</param>
/// <param name="MainFrameId">WebView2's id for the browser's main frame; 0 before its first navigation.</param>
/// <param name="Host">The page's host; empty when it has none.</param>
internal sealed record OdWebPage(string ModuleId, uint MainFrameId, string Host);

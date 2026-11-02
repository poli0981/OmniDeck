namespace OmniDeck.Web.Runtime;

/// <summary>A WebView2 process and the main frames whose pages it hosts; the browser, GPU and utility processes host none.</summary>
internal sealed record OdWebProcess(int Id, IReadOnlyList<OdWebFrame> MainFrames);

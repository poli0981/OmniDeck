namespace OmniDeck.Core.Governor;

/// <summary>
/// Measures processes OmniDeck runs besides its own, for the governor (docs/RAM-GOVERNOR.md §4): the
/// web runner reports the WebView2 processes. Called on the UI thread, once per pass.
/// </summary>
public interface IOdMemoryReporter
{
    Task<OdMemoryReport> ReportAsync();
}

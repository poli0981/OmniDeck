namespace OmniDeck.App.Diagnostics;

/// <summary>
/// Where the error dialog asks for a bug report about the error it showed; the main window, which
/// shows the report dialog, answers. It keeps the crash handler free of the report's many services.
/// </summary>
public sealed class OdBugReportRequests
{
    public event EventHandler<OdBugReportRequestedEventArgs>? Requested;

    public void Request(string? error) => Requested?.Invoke(this, new OdBugReportRequestedEventArgs(error));
}

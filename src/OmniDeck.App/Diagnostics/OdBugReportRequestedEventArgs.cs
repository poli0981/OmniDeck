namespace OmniDeck.App.Diagnostics;

/// <summary>A bug report was asked for, about <see cref="Error"/> when it came from the error dialog.</summary>
public sealed class OdBugReportRequestedEventArgs(string? error) : EventArgs
{
    public string? Error { get; } = error;
}

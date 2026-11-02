namespace OmniDeck.Web.Diagnostics;

/// <summary>One line a web app's page wrote to its console; <c>Level</c> is log, info, debug, warning or error.</summary>
public sealed record OdConsoleEntry(DateTimeOffset Time, string Level, string Message)
{
    public bool IsError => string.Equals(Level, OdConsoleLog.Error, StringComparison.Ordinal);
}

using OmniDeck.Core.Governor;

namespace OmniDeck.Tests.Governor;

/// <summary>A memory reporter the test sets, as the web runner reports its processes.</summary>
public sealed class OdFakeReporter : IOdMemoryReporter
{
    public long Shared { get; set; }

    public IDictionary<string, long> Modules { get; } = new Dictionary<string, long>(StringComparer.Ordinal);

    /// <summary>Makes the next reports fail, as when the browser process is restarting.</summary>
    public bool Fails { get; set; }

    public Task<OdMemoryReport> ReportAsync() => Fails
        ? Task.FromException<OdMemoryReport>(new InvalidOperationException("The browser is restarting"))
        : Task.FromResult(new OdMemoryReport(Shared, new Dictionary<string, long>(Modules, StringComparer.Ordinal)));
}

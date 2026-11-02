namespace OmniDeck.Core.Governor;

/// <summary>What one <see cref="IOdMemoryReporter"/> measured, as private commit.</summary>
/// <param name="SharedBytes">Processes no single module owns, such as WebView2's browser and GPU processes.</param>
/// <param name="Modules">Each module's own processes, by module id; an open module whose processes are not up yet has 0.</param>
public sealed record OdMemoryReport(long SharedBytes, IReadOnlyDictionary<string, long> Modules)
{
    /// <summary>Nothing running.</summary>
    public static OdMemoryReport None { get; } = new(0, new Dictionary<string, long>(StringComparer.Ordinal));
}

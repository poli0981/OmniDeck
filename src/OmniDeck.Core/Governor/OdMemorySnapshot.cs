namespace OmniDeck.Core.Governor;

/// <summary>
/// The private commit one governor pass measured (docs/RAM-GOVERNOR.md §4): OmniDeck's own process,
/// the processes the web apps share, and each web app's own.
/// </summary>
/// <param name="ShellBytes">OmniDeck's own process, which the built-in modules share.</param>
/// <param name="SharedBytes">Processes no single module owns: WebView2's browser, GPU and utility processes.</param>
/// <param name="Modules">What each module that runs processes of its own uses, by module id.</param>
public sealed record OdMemorySnapshot(long ShellBytes, long SharedBytes, IReadOnlyDictionary<string, long> Modules)
{
    public long TotalBytes => ShellBytes + SharedBytes + Modules.Values.Sum();

    /// <summary>What <paramref name="moduleId"/> uses on its own; null for a module that runs in OmniDeck's process.</summary>
    public long? For(string moduleId) => Modules.TryGetValue(moduleId, out var bytes) ? bytes : null;

    /// <summary>
    /// The figures once <paramref name="moduleId"/> has let go of its processes, which takes them a
    /// moment to exit, so the next pass measures again; with no web app left, the shared processes exit
    /// too. <paramref name="shellBytes"/> is OmniDeck's own process, measured again.
    /// </summary>
    internal OdMemorySnapshot Without(string moduleId, long shellBytes)
    {
        if (!Modules.ContainsKey(moduleId))
        {
            return this with { ShellBytes = shellBytes };
        }

        var modules = Modules.Where(pair => !string.Equals(pair.Key, moduleId, StringComparison.Ordinal)).ToDictionary(StringComparer.Ordinal);
        return new OdMemorySnapshot(shellBytes, modules.Count > 0 ? SharedBytes : 0, modules);
    }
}

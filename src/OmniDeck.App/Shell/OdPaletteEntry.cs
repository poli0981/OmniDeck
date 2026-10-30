namespace OmniDeck.App.Shell;

/// <summary>One command palette result: a module to open, or a shell command.</summary>
/// <param name="Title">What the palette matches and shows.</param>
/// <param name="Detail">The second line: the module's category, or "Command".</param>
/// <param name="Icon">A WPF-UI <c>SymbolRegular</c> name.</param>
/// <param name="RunAsync">What choosing the entry does.</param>
public sealed record OdPaletteEntry(string Title, string Detail, string Icon, Func<Task> RunAsync);

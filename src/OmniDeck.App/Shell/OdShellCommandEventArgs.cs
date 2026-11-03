namespace OmniDeck.App.Shell;

/// <summary>Which <see cref="OdShellCommand"/> the palette ran.</summary>
public sealed class OdShellCommandEventArgs(OdShellCommand command) : EventArgs
{
    public OdShellCommand Command { get; } = command;
}

namespace OmniDeck.Modules.Media;

/// <summary>A file could not be played; <see cref="File"/> is its name.</summary>
internal sealed class OdMediaFailedEventArgs(string file) : EventArgs
{
    public string File { get; } = file;
}

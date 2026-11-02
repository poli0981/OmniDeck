namespace OmniDeck.Modules.Media;

/// <summary>One file in the queue, as the list shows it.</summary>
/// <param name="Index">Its place in the queue.</param>
/// <param name="Path">The file.</param>
/// <param name="IsCurrent">Whether it is the one playing, or next to play.</param>
public sealed record OdMediaItem(int Index, string Path, bool IsCurrent)
{
    public string Title => System.IO.Path.GetFileNameWithoutExtension(Path);

    public override string ToString() => Title;
}

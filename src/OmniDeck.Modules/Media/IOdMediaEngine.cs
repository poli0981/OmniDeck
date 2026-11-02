namespace OmniDeck.Modules.Media;

/// <summary>Makes players: libVLC in the app (<see cref="OdVlcEngine"/>). Disposing it lets libVLC go.</summary>
internal interface IOdMediaEngine : IDisposable
{
    IOdMediaPlayer CreatePlayer();
}

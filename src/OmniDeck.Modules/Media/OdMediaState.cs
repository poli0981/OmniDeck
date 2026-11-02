namespace OmniDeck.Modules.Media;

/// <summary>Where a player stands.</summary>
internal enum OdMediaState
{
    Idle,
    Opening,
    Playing,
    Paused,
    Stopped,
    Ended,
    Failed,
}

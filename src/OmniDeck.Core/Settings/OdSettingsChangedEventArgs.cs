namespace OmniDeck.Core.Settings;

/// <summary>Carries the settings as they are after a change.</summary>
public sealed class OdSettingsChangedEventArgs(OdSettings settings) : EventArgs
{
    public OdSettings Settings { get; } = settings;
}

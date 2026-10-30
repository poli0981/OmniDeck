namespace OmniDeck.App.Settings;

/// <summary>Something asked for a settings tab, such as the palette's "Running modules".</summary>
public sealed class OdSettingsSectionEventArgs(OdSettingsSection section) : EventArgs
{
    public OdSettingsSection Section { get; } = section;
}

namespace OmniDeck.Core.Settings;

/// <summary>The app's settings: read anywhere, changed through <see cref="Update"/>, saved at once.</summary>
public interface IOdSettingsService
{
    /// <summary>Raised after every change, with the new settings.</summary>
    event EventHandler<OdSettingsChangedEventArgs>? Changed;

    OdSettings Current { get; }

    /// <summary>Applies <paramref name="change"/>, normalizes the result, saves it and raises <see cref="Changed"/>.</summary>
    void Update(Func<OdSettings, OdSettings> change);

    /// <summary>Writes the current settings to <paramref name="path"/>, for Settings → Data → Export.</summary>
    void Export(string path);

    /// <summary>Replaces the settings with the file at <paramref name="path"/>; false when it is not a settings file.</summary>
    bool Import(string path);
}

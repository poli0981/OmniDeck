namespace OmniDeck.Core.Settings;

/// <summary>The app's settings: read anywhere, changed through <see cref="Update"/>, saved at once.</summary>
public interface IOdSettingsService
{
    /// <summary>Raised after every change, with the new settings.</summary>
    event EventHandler<OdSettingsChangedEventArgs>? Changed;

    OdSettings Current { get; }

    /// <summary>Applies <paramref name="change"/>, normalizes the result, saves it and raises <see cref="Changed"/>.</summary>
    void Update(Func<OdSettings, OdSettings> change);
}

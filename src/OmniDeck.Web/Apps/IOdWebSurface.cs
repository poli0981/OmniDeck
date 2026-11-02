namespace OmniDeck.Web.Apps;

/// <summary>What <see cref="OdWebAppModule"/> needs from its view's browser; tests stand in for WebView2.</summary>
internal interface IOdWebSurface
{
    /// <summary>The page on screen; null before the browser is ready.</summary>
    Uri? Source { get; }

    bool IsPlayingAudio { get; }

    /// <summary>Lowers the browser's memory use and freezes its page; the view is already off screen.</summary>
    Task SuspendAsync();

    /// <summary>Undoes <see cref="SuspendAsync"/>.</summary>
    void Resume();

    /// <summary>How many cookies the profile holds for <paramref name="page"/>; null before the browser is ready.</summary>
    Task<int?> CountCookiesAsync(Uri page);

    /// <summary>Closes the browser for good.</summary>
    void Close();
}

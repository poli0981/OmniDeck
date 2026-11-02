using System.Net.Http;
using OmniDeck.Core.Storage;
using Serilog;

namespace OmniDeck.Core.Modules;

/// <summary>What the shell gives each module; modules never construct these themselves.</summary>
public interface IOdModuleContext
{
    string ModuleId { get; }

    /// <summary>The settings document that belongs to this module.</summary>
    IOdModuleSettings Settings { get; }

    ILogger Log { get; }

    TimeProvider Time { get; }

    /// <summary>
    /// Windows notifications, raised from the tray icon so they work while minimized. Clicking one opens
    /// this module.
    /// </summary>
    IOdNotifier Notifier { get; }

    /// <summary>
    /// The HTTP client OmniDeck's own features share. It names OmniDeck in its User-Agent and keeps no
    /// cookies; a module may only call the services docs/legal/PRIVACY.md lists for it.
    /// </summary>
    HttpClient Http { get; }

    /// <summary>
    /// The app database, for the tables a module keeps. Their migrations ship with Core
    /// (<see cref="OdCoreMigrations"/>), so one schema version covers the whole file.
    /// </summary>
    OdDatabase Database { get; }
}

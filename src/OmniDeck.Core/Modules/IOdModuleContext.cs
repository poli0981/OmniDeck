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

    /// <summary>Windows notifications, raised from the tray icon so they work while minimized.</summary>
    IOdNotifier Notifier { get; }
}

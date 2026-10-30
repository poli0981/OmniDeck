using Serilog;

namespace OmniDeck.Core.Modules;

/// <summary>The context <see cref="OdModuleHost"/> hands a module when it initializes.</summary>
internal sealed record OdModuleContext(string ModuleId, IOdModuleSettings Settings, ILogger Log, TimeProvider Time, IOdNotifier Notifier)
    : IOdModuleContext;

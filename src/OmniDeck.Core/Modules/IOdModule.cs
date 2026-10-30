using System.Windows;

namespace OmniDeck.Core.Modules;

/// <summary>
/// The module contract (docs/ARCHITECTURE.md §2). The shell's module host calls these in order; the
/// RAM governor decides when to suspend and hibernate. Durable state belongs in services, not views,
/// so hibernating a view never loses it.
/// </summary>
public interface IOdModule
{
    OdModuleInfo Info { get; }

    /// <summary>Called once per process lifetime, before the first view.</summary>
    Task InitializeAsync(IOdModuleContext context);

    /// <summary>Called on first open and again after every hibernation.</summary>
    FrameworkElement CreateView();

    /// <summary>The module became visible.</summary>
    Task ActivateAsync();

    /// <summary>Returning <see langword="false"/> pins the module, for example while media plays.</summary>
    Task<bool> CanSuspendAsync();

    /// <summary>Hidden or under memory pressure: drop caches, charts and bitmaps; stop timers and animations.</summary>
    Task SuspendAsync(OdSuspendReason reason);

    /// <summary>Release everything, view included; keep only a tiny state blob.</summary>
    Task HibernateAsync();

    /// <summary>The user closed the module.</summary>
    Task CloseAsync();
}

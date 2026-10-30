using System.Windows;

namespace OmniDeck.Core.Modules;

/// <summary>A module as <see cref="OdModuleHost"/> runs it: its state, its view while it has one, and when it was last seen.</summary>
public sealed class OdHostedModule
{
    internal OdHostedModule(IOdModule module)
    {
        Module = module;
    }

    public IOdModule Module { get; }

    public OdModuleInfo Info => Module.Info;

    public OdModuleState State { get; internal set; } = OdModuleState.Created;

    /// <summary>The module's view; <see langword="null"/> before the first open and after hibernation or closing.</summary>
    public FrameworkElement? View { get; internal set; }

    /// <summary>When the module was last visible: the governor measures how long it has been hidden from here.</summary>
    public DateTimeOffset LastActive { get; internal set; }

    internal bool IsInitialized { get; set; }
}

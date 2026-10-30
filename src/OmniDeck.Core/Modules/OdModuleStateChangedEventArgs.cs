namespace OmniDeck.Core.Modules;

/// <summary>A module moved to another rung of the state ladder.</summary>
public sealed class OdModuleStateChangedEventArgs(OdHostedModule module, OdModuleState previous) : EventArgs
{
    public OdHostedModule Module { get; } = module;

    public OdModuleState Previous { get; } = previous;
}

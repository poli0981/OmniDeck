namespace OmniDeck.Core.Modules;

/// <summary>The governor's ladder (docs/RAM-GOVERNOR.md §1).</summary>
public enum OdModuleState
{
    /// <summary>Known to the shell but never opened.</summary>
    Created,

    /// <summary>Visible, or interacted with.</summary>
    Active,

    /// <summary>Hidden for less than <c>IdleAfter</c>; still live.</summary>
    Idle,

    /// <summary>Hidden for longer: caches, timers and animations released.</summary>
    Suspended,

    /// <summary>View and everything else released; only a tiny state blob is kept.</summary>
    Hibernated,

    /// <summary>Closed by the user.</summary>
    Closed,
}

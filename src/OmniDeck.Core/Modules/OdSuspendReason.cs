namespace OmniDeck.Core.Modules;

/// <summary>Why a module is being suspended.</summary>
public enum OdSuspendReason
{
    /// <summary>It has been hidden for <c>IdleAfter</c>.</summary>
    Hidden,

    /// <summary>The governor needs memory back.</summary>
    MemoryPressure,

    /// <summary>The user asked, from the task-manager panel.</summary>
    User,
}

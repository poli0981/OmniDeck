namespace OmniDeck.Web.Navigation;

/// <summary>Where a navigation or a popup goes.</summary>
public enum OdNavDecision
{
    /// <summary>It stays in the web app.</summary>
    Allow,

    /// <summary>It opens in the default browser.</summary>
    OpenExternally,

    /// <summary>It goes nowhere: an app protocol such as <c>spotify:</c>.</summary>
    Block,
}

namespace OmniDeck.Web.Navigation;

/// <summary>A link a web app tried to open.</summary>
public sealed class OdLinkEventArgs(Uri target) : EventArgs
{
    public Uri Target { get; } = target;
}

using System.Resources;

namespace OmniDeck.Web.Resources;

/// <summary>The web runner's string table, <c>Resources/Strings.resx</c>; the app registers it with <c>OdLocalization</c>.</summary>
public static class OdWebStrings
{
    public static ResourceManager Resources { get; } = new("OmniDeck.Web.Resources.Strings", typeof(OdWebStrings).Assembly);
}

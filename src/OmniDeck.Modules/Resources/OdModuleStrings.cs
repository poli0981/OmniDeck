using System.Resources;

namespace OmniDeck.Modules.Resources;

/// <summary>The built-in modules' string table, <c>Resources/Strings.resx</c>; the app registers it with <c>OdLocalization</c>.</summary>
public static class OdModuleStrings
{
    public static ResourceManager Resources { get; } = new("OmniDeck.Modules.Resources.Strings", typeof(OdModuleStrings).Assembly);
}

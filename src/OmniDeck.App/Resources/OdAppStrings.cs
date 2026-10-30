using System.Resources;

namespace OmniDeck.App.Resources;

/// <summary>The shell's string table, <c>Resources/Strings.resx</c>; <c>OdApp</c> registers it with <c>OdLocalization</c>.</summary>
internal static class OdAppStrings
{
    public static ResourceManager Resources { get; } = new("OmniDeck.App.Resources.Strings", typeof(OdAppStrings).Assembly);
}

using OmniDeck.App.Resources;
using OmniDeck.Core.Localization;
using OmniDeck.Tests.TestSupport;

[assembly: AssemblyFixture(typeof(OdAppStringsFixture))]

namespace OmniDeck.Tests.TestSupport;

/// <summary>Registers the shell's string table once for the whole run, as <c>OdApp</c> does at startup.</summary>
public sealed class OdAppStringsFixture
{
    public OdAppStringsFixture()
    {
        OdLocalization.Instance.Register(OdAppStrings.Resources);
    }
}

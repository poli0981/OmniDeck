using System.Globalization;
using OmniDeck.App.Resources;
using OmniDeck.Core.Localization;
using OmniDeck.Modules.Resources;
using OmniDeck.Tests.TestSupport;
using OmniDeck.Web.Resources;

[assembly: AssemblyFixture(typeof(OdAppStringsFixture))]

namespace OmniDeck.Tests.TestSupport;

/// <summary>
/// Registers the shell's string table once for the whole run, as <c>OdApp</c> does at startup, and runs
/// every test in US English, UI and regional formats alike: the tests read English strings and US
/// dates, whatever the machine's own languages.
/// </summary>
public sealed class OdAppStringsFixture
{
    public OdAppStringsFixture()
    {
        var english = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = english;
        CultureInfo.DefaultThreadCurrentUICulture = english;
        CultureInfo.CurrentCulture = english;
        CultureInfo.CurrentUICulture = english;
        OdLocalization.Instance.SetCulture(english);
        OdLocalization.Instance.Register(OdAppStrings.Resources);
        OdLocalization.Instance.Register(OdModuleStrings.Resources);
        OdLocalization.Instance.Register(OdWebStrings.Resources);
    }
}

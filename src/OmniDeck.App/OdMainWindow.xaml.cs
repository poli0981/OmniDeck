using OmniDeck.Core;

namespace OmniDeck.App;

/// <summary>Placeholder shell window of the kickoff build; the real shell arrives in P1.</summary>
public partial class OdMainWindow
{
    public OdMainWindow()
    {
        InitializeComponent();
        VersionText.Text = OdVersionInfo.FromAssembly(typeof(OdMainWindow).Assembly).DisplayText;
    }
}

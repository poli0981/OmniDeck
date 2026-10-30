namespace OmniDeck.App.Shell;

/// <summary>
/// The task-manager panel, bound to <see cref="OdMemoryViewModel"/>. The sidebar footer opens it until
/// Settings → Memory exists (docs/RAM-GOVERNOR.md §5).
/// </summary>
public partial class OdMemoryPage
{
    public OdMemoryPage()
    {
        InitializeComponent();
    }
}

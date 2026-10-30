namespace OmniDeck.App.Shell;

/// <summary>
/// The one page every module item navigates to: it shows <see cref="OdShellViewModel.CurrentView"/>,
/// so the navigation view never creates, caches or keeps a module view (docs/WPFUI-CONVENTIONS.md §5).
/// </summary>
public partial class OdModuleStage
{
    public OdModuleStage()
    {
        InitializeComponent();
    }
}

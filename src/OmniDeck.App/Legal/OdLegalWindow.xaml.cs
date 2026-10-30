namespace OmniDeck.App.Legal;

/// <summary>
/// Shows the legal documents: as the gate before the main window (first run, and after each legal
/// update), or opened again from About to read them.
/// </summary>
public partial class OdLegalWindow
{
    private bool _isGate;

    public OdLegalWindow(OdLegalViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Finished += (_, _) =>
        {
            if (_isGate)
            {
                DialogResult = viewModel.Accepted;
            }
            else
            {
                Close();
            }
        };
    }

    /// <summary>Shows the gate and waits: true once the user continued; closing the window counts as Quit.</summary>
    public bool ShowGate()
    {
        _isGate = true;
        return ShowDialog() == true;
    }
}

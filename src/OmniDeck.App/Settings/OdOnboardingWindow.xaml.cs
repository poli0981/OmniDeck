namespace OmniDeck.App.Settings;

/// <summary>First-run setup, between the legal gate and the main window; closing it keeps the defaults.</summary>
public partial class OdOnboardingWindow
{
    public OdOnboardingWindow(OdOnboardingViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Finished += (_, _) => Close();
    }
}

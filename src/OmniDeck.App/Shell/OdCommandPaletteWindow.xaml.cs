using System.Windows;
using System.Windows.Input;

namespace OmniDeck.App.Shell;

/// <summary>The command palette overlay (Ctrl+K). After it closes, <see cref="OdCommandPaletteViewModel.Chosen"/> holds the pick.</summary>
public partial class OdCommandPaletteWindow
{
    // Sits near the top of its owner, where a search box is expected.
    private const double TopOffset = 72;

    private readonly OdCommandPaletteViewModel _viewModel;
    private bool _closing;

    public OdCommandPaletteWindow(OdCommandPaletteViewModel viewModel, Window owner)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(owner);
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Owner = owner;
        viewModel.Accepted += (_, _) => RequestClose();
        Loaded += (_, _) =>
        {
            var bounds = owner.WindowState == WindowState.Maximized ? owner.RestoreBounds : new Rect(owner.Left, owner.Top, owner.ActualWidth, owner.ActualHeight);
            Left = bounds.Left + ((bounds.Width - ActualWidth) / 2);
            Top = bounds.Top + TopOffset;
            SearchBox.Focus();
        };

        // Clicking away closes it. Closing deactivates the window too, whoever closes it (Esc, Alt+F4,
        // the owner), so a close already under way must not be started again.
        Closing += (_, _) => _closing = true;
        Deactivated += (_, _) => RequestClose();
    }

    private void RequestClose()
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        Close();
    }

    private void OnResultDoubleClick(object sender, MouseButtonEventArgs e) => _viewModel.AcceptCommand.Execute(null);

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                RequestClose();
                break;
            case Key.Enter:
                _viewModel.AcceptCommand.Execute(null);
                break;
            case Key.Down:
                MoveSelection(1);
                break;
            case Key.Up:
                MoveSelection(-1);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void MoveSelection(int delta)
    {
        _viewModel.MoveSelection(delta);
        if (_viewModel.Selected is { } selected)
        {
            ResultsList.ScrollIntoView(selected);
        }
    }
}

using System.Windows;

namespace OmniDeck.App.Shell;

/// <summary>A module's own window, to open with the module's view, bring to the front, or close.</summary>
public sealed class OdPopOutEventArgs(OdModuleItemViewModel item, FrameworkElement? view = null) : EventArgs
{
    public OdModuleItemViewModel Item { get; } = item;

    /// <summary>The module's view, for a window that opens; null otherwise.</summary>
    public FrameworkElement? View { get; } = view;
}

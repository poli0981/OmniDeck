using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace OmniDeck.App.Shell;

/// <summary>
/// A sidebar item screen readers can use: a list item that says whether its page is on screen, and that
/// they can select or invoke. WPF-UI's own item peer never reaches them, because the sidebar's list wraps
/// each item in a data item that has neither (<see cref="OdNavigationView"/> lists these instead).
/// </summary>
public sealed class OdNavItem : NavigationViewItem
{
    /// <summary>Navigates as a click does.</summary>
    internal void Open() => OnClick();

    protected override AutomationPeer OnCreateAutomationPeer() => new OdNavItemAutomationPeer(this);

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsActiveProperty && UIElementAutomationPeer.FromElement(this) is { } peer)
        {
            peer.RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, e.OldValue, e.NewValue);
            if (e.NewValue is true)
            {
                peer.RaiseAutomationEvent(AutomationEvents.SelectionItemPatternOnElementSelected);
            }
        }
    }

    private sealed class OdNavItemAutomationPeer(OdNavItem owner) : FrameworkElementAutomationPeer(owner), IInvokeProvider, ISelectionItemProvider
    {
        public bool IsSelected => owner.IsActive;

        public IRawElementProviderSimple? SelectionContainer => GetParent() is { } list ? ProviderFromPeer(list) : null;

        public override object? GetPattern(PatternInterface patternInterface) =>
            patternInterface is PatternInterface.Invoke or PatternInterface.SelectionItem ? this : base.GetPattern(patternInterface);

        public void Invoke()
        {
            if (!IsEnabled())
            {
                throw new ElementNotEnabledException();
            }

            // As WPF's own buttons do: the client's call returns before the navigation runs.
            _ = owner.Dispatcher.BeginInvoke(DispatcherPriority.Input, owner.Open);
        }

        public void Select() => Invoke();

        public void AddToSelection() => throw new InvalidOperationException();

        public void RemoveFromSelection() => throw new InvalidOperationException();

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override string GetClassNameCore() => nameof(OdNavItem);

        // The name says it all: the icon's glyph and the label would only repeat it.
        protected override List<AutomationPeer>? GetChildrenCore() => null;
    }
}

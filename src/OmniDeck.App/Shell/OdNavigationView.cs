using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using OmniDeck.Core.Localization;
using Wpf.Ui.Controls;

namespace OmniDeck.App.Shell;

/// <summary>
/// WPF-UI's navigation view, with a peer that gives screen readers the sidebar as two lists of
/// <see cref="OdNavItem"/>: the modules, then Settings and the library. WPF-UI hosts the items in plain
/// item controls, whose peers wrap each item in a data item that can be neither selected nor invoked.
/// </summary>
public sealed class OdNavigationView : NavigationView
{
    public OdNavigationView()
    {
        // Modules come and go from the sidebar; screen readers then read the lists again.
        foreach (var items in new[] { MenuItems, FooterMenuItems })
        {
            if (items is INotifyCollectionChanged changes)
            {
                changes.CollectionChanged += (_, _) => (UIElementAutomationPeer.FromElement(this) as OdNavigationViewAutomationPeer)?.ListsChanged();
            }
        }
    }

    /// <summary>The template's list of modules; WPF-UI keeps it protected.</summary>
    internal ItemsControl? Menu => MenuItemsItemsControl;

    /// <summary>The template's list at the foot of the sidebar.</summary>
    internal ItemsControl? Footer => FooterMenuItemsItemsControl;

    protected override AutomationPeer OnCreateAutomationPeer() => new OdNavigationViewAutomationPeer(this);

    private sealed class OdNavigationViewAutomationPeer(OdNavigationView owner) : FrameworkElementAutomationPeer(owner)
    {
        private OdNavListAutomationPeer? _menu;
        private OdNavListAutomationPeer? _footer;

        internal void ListsChanged()
        {
            _menu?.InvalidatePeer();
            _footer?.InvalidatePeer();
        }

        // Not an element of its own: the pane button, the lists and the page stay beside the title bar.
        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;

        // As UIElementAutomationPeer walks the visual tree, except that the two item controls (and so
        // their wrapping peers) are replaced by lists of the items' own peers.
        protected override List<AutomationPeer> GetChildrenCore()
        {
            var children = new List<AutomationPeer>();
            Collect(owner, children);
            return children;
        }

        private void Collect(DependencyObject parent, List<AutomationPeer> children)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (ReferenceEquals(child, owner.Menu))
                {
                    children.Add(ListFor(ref _menu, owner.Menu, () => OdLocalization.Get("Shell_NavModules")));
                }
                else if (ReferenceEquals(child, owner.Footer))
                {
                    children.Add(ListFor(ref _footer, owner.Footer, () => OdLocalization.Get("Shell_NavFooter")));
                }
                else if (child is UIElement element && !HoldsAList(element) && CreatePeerForElement(element) is { } peer)
                {
                    children.Add(peer);
                }
                else
                {
                    Collect(child, children);
                }
            }
        }

        // The lists sit in a scroll viewer, whose peer would list them wrapped as WPF-UI does.
        private bool HoldsAList(UIElement element) =>
            owner.Menu?.IsDescendantOf(element) == true || owner.Footer?.IsDescendantOf(element) == true;

        private static OdNavListAutomationPeer ListFor(ref OdNavListAutomationPeer? cached, ItemsControl list, Func<string> name)
        {
            // A new template brings new item controls, and so new lists.
            if (cached?.Owner != list)
            {
                cached = new OdNavListAutomationPeer(list, name);
            }

            return cached;
        }
    }

    /// <summary>One of the sidebar's lists: the items' own peers, and the one whose page is on screen.</summary>
    private sealed class OdNavListAutomationPeer(ItemsControl list, Func<string> name) : FrameworkElementAutomationPeer(list), ISelectionProvider
    {
        public bool CanSelectMultiple => false;

        public bool IsSelectionRequired => false;

        public IRawElementProviderSimple[] GetSelection() =>
            [.. Items().Where(item => item.IsActive).Select(CreatePeerForElement).OfType<AutomationPeer>().Select(ProviderFromPeer)];

        public override object? GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.Selection ? this : base.GetPattern(patternInterface);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override string GetClassNameCore() => "OdNavList";

        protected override string GetNameCore() => name();

        protected override List<AutomationPeer> GetChildrenCore() => [.. Items().Select(CreatePeerForElement).OfType<AutomationPeer>()];

        private IEnumerable<OdNavItem> Items() => list.Items.OfType<OdNavItem>();
    }
}

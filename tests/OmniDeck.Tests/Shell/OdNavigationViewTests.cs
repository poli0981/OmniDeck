using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using OmniDeck.App.Shell;
using OmniDeck.Tests.TestSupport;
using Wpf.Ui.Controls;

namespace OmniDeck.Tests.Shell;

public sealed class OdNavigationViewTests
{
    [Fact]
    public void ScreenReaders_GetTheSidebarAsTwoListsOfItems() => OdSta.Run(() =>
    {
        var view = NewView(out var clock, out _, out var settings);

        var peer = UIElementAutomationPeer.CreatePeerForElement(view);
        var lists = peer.GetChildren().Where(child => child.GetAutomationControlType() == AutomationControlType.List).ToList();

        // Not an element of its own: its lists sit beside the window's other parts.
        Assert.False(peer.IsControlElement());
        Assert.Equal(new[] { "Modules", "Settings and library" }, lists.Select(list => list.GetName()), StringComparer.Ordinal);
        var modules = lists[0].GetChildren();
        Assert.Equal(new[] { "Clock", "Timer" }, modules.Select(item => item.GetName()), StringComparer.Ordinal);
        Assert.All(modules, item => Assert.Equal(AutomationControlType.ListItem, item.GetAutomationControlType()));
        Assert.Empty(modules[0].GetChildren() ?? []);
        Assert.NotNull(modules[0].GetPattern(PatternInterface.Invoke));
        Assert.Equal("Settings", Assert.Single(lists[1].GetChildren()).GetName());

        var selection = (ISelectionItemProvider)modules[0].GetPattern(PatternInterface.SelectionItem);
        Assert.False(selection.IsSelected);
        clock.IsActive = true;
        Assert.True(selection.IsSelected);

        // The list that holds it (providers themselves need a window, which a test has not).
        Assert.Same(lists[0], modules[0].GetParent());
        Assert.Single(((ISelectionProvider)lists[0].GetPattern(PatternInterface.Selection)).GetSelection());
        Assert.False(settings.IsActive);
        return Task.CompletedTask;
    });

    [Fact]
    public void TheListsFollowTheSidebar() => OdSta.Run(() =>
    {
        var view = NewView(out _, out var timer, out _);
        var peer = UIElementAutomationPeer.CreatePeerForElement(view);
        var modules = peer.GetChildren().First(child => child.GetAutomationControlType() == AutomationControlType.List);

        view.MenuItems.Remove(timer);
        modules.ResetChildrenCache();

        Assert.Equal("Clock", Assert.Single(modules.GetChildren()).GetName());
        return Task.CompletedTask;
    });

    [Fact]
    public void InvokingAnItem_NavigatesAsAClickDoes() => OdSta.Run(async () =>
    {
        var view = NewView(out var clock, out _, out _);
        var clicked = 0;
        clock.Click += (_, _) => clicked++;

        ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(clock).GetPattern(PatternInterface.Invoke)).Invoke();
        await Task.Yield();
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        Assert.Equal(1, clicked);
        Assert.NotNull(view.Menu);
    });

    /// <summary>A navigation view as the main window builds it, on WPF-UI's own dictionaries, laid out.</summary>
    private static OdNavigationView NewView(out OdNavItem clock, out OdNavItem timer, out OdNavItem settings)
    {
        // Registers the pack:// scheme, which an Application otherwise does.
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
        var host = new Grid();
        host.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/Wpf.Ui;component/Resources/Theme/Dark.xaml", UriKind.Relative)));
        host.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/Wpf.Ui;component/Resources/Wpf.Ui.xaml", UriKind.Relative)));

        var view = new OdNavigationView { Style = (Style)host.FindResource(typeof(NavigationView)) };
        host.Children.Add(view);
        clock = Item(host, "Clock");
        timer = Item(host, "Timer");
        settings = Item(host, "Settings");
        view.MenuItems.Add(clock);
        view.MenuItems.Add(timer);
        view.FooterMenuItems.Add(settings);
        host.Measure(new Size(800, 600));
        host.Arrange(new Rect(0, 0, 800, 600));
        return view;
    }

    private static OdNavItem Item(FrameworkElement host, string name)
    {
        var item = new OdNavItem { Style = (Style)host.FindResource(typeof(NavigationViewItem)), Content = name, TargetPageTag = name };
        AutomationProperties.SetName(item, name);
        return item;
    }
}

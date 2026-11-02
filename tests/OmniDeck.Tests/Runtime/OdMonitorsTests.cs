using System.Windows;
using System.Windows.Interop;
using OmniDeck.Core.Runtime;
using OmniDeck.Tests.TestSupport;

namespace OmniDeck.Tests.Runtime;

public sealed class OdMonitorsTests
{
    [Fact]
    public void BoundsOf_AWindow_IsTheMonitorItIsOn() => OdSta.Run(() =>
    {
        // A window that is never shown is still near a monitor.
        var window = new Window();
        var bounds = OdMonitors.BoundsOf(new WindowInteropHelper(window).EnsureHandle());

        Assert.False(bounds.IsEmpty);
        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
        window.Close();
        return Task.CompletedTask;
    });
}

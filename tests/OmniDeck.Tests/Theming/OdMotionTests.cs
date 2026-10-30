using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OmniDeck.App.Theming;
using OmniDeck.Core.Theming;
using OmniDeck.Tests.TestSupport;

namespace OmniDeck.Tests.Theming;

public sealed class OdMotionTests
{
    [Fact]
    public void Duration_ComesFromTheToken_AndIsZeroWithoutOne() => OdSta.Run(() =>
    {
        var element = new Border();
        Assert.Equal(TimeSpan.Zero, OdMotion.DurationFor(element));

        element.Resources[OdTokenKeys.MotionDuration] = new Duration(TimeSpan.FromMilliseconds(180));

        Assert.Equal(TimeSpan.FromMilliseconds(180), OdMotion.DurationFor(element));
        return Task.CompletedTask;
    });

    [Fact]
    public void HoverScale_JumpsWithoutMotion_AndAnimatesWithIt() => OdSta.Run(() =>
    {
        var element = new Border();
        OdMotion.SetHoverScale(element, true);
        Assert.True(OdMotion.GetHoverScale(element));
        var scale = Assert.IsType<ScaleTransform>(element.RenderTransform);

        Hover(element, UIElement.MouseEnterEvent);
        Assert.Equal(1.03, scale.ScaleX, 3);
        Assert.False(scale.HasAnimatedProperties);
        Hover(element, UIElement.MouseLeaveEvent);
        Assert.Equal(1.0, scale.ScaleY, 3);

        element.Resources[OdTokenKeys.MotionDuration] = new Duration(TimeSpan.FromMilliseconds(180));
        Hover(element, UIElement.MouseEnterEvent);
        Assert.True(scale.HasAnimatedProperties);
        return Task.CompletedTask;
    });

    private static void Hover(UIElement element, RoutedEvent routedEvent) =>
        element.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = routedEvent });
}

using System.Windows.Controls;
using OmniDeck.Core.Modules;
using OmniDeck.Tests.TestSupport;

namespace OmniDeck.Tests.Modules;

public sealed class OdFullscreenTests
{
    [Fact]
    public void AViewsRequest_BubblesUpToItsWindow_AndTheAnswerFlowsDownToIt() => OdSta.Run(() =>
    {
        var view = new Border();
        var stage = new ContentControl { Content = view };
        var window = new Grid { Children = { stage } };
        var requests = new List<bool>();
        window.AddHandler(OdFullscreen.RequestedEvent, new EventHandler<OdFullscreenRequestedEventArgs>((_, e) => requests.Add(e.On)));

        OdFullscreen.Request(view, on: true);
        OdFullscreen.SetIsActive(stage, true);

        Assert.Equal(new[] { true }, requests);
        Assert.True(OdFullscreen.GetIsActive(view));

        OdFullscreen.Request(view, on: false);
        OdFullscreen.SetIsActive(stage, false);
        Assert.Equal(new[] { true, false }, requests);
        Assert.False(OdFullscreen.GetIsActive(view));
        return Task.CompletedTask;
    });
}

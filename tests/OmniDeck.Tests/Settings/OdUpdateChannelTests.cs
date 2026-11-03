using OmniDeck.Core.Settings;

namespace OmniDeck.Tests.Settings;

public sealed class OdUpdateChannelTests
{
    [Theory]
    [InlineData(null, "0.2.0-alpha.4", true)]
    [InlineData(null, "0.9.0", true)]
    [InlineData(null, "1.0.0", false)]
    [InlineData(null, "1.1.0-beta.1", true)]
    [InlineData(null, "1.2.3+abcdef0", false)]
    [InlineData(null, "unknown", true)]
    [InlineData(false, "0.3.0-alpha.1", false)]
    [InlineData(true, "1.0.0", true)]
    public void TheChannel_FollowsTheRunningBuild_UnlessTheUserChose(bool? choice, string running, bool prereleases) =>
        Assert.Equal(prereleases, OdUpdateChannel.IncludesPrereleases(choice, running));
}

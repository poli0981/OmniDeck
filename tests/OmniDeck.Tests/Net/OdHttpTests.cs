using OmniDeck.Core;
using OmniDeck.Core.Net;

namespace OmniDeck.Tests.Net;

public sealed class OdHttpTests
{
    [Fact]
    public void TheSharedClient_NamesOmniDeck_AndGivesUpAfterTheTimeout()
    {
        using var client = OdHttp.Create(new OdVersionInfo("0.2.0-alpha.1", "abc1234"));

        Assert.Equal("OmniDeck/0.2.0-alpha.1 (+https://github.com/poli0981/OmniDeck)", client.DefaultRequestHeaders.UserAgent.ToString());
        Assert.Equal(OdHttp.Timeout, client.Timeout);
    }
}

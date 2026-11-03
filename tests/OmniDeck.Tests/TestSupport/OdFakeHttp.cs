namespace OmniDeck.Tests.TestSupport;

/// <summary>Answers HTTP requests without the network, and keeps every request it was sent.</summary>
public sealed class OdFakeHttp(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
{
    public IList<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(answer(request));
    }
}

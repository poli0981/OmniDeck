namespace OmniDeck.Tests.TestSupport;

/// <summary>One HTTP client for tests that build a module host; none of them sends a request.</summary>
public static class OdTestHttp
{
    public static HttpClient Client { get; } = new();
}

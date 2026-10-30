namespace OmniDeck.Tests.Localization;

/// <summary>Tests that switch the process-wide UI culture run alone, after the parallel ones.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class OdCultureCollection
{
    public const string Name = "Culture";

    private OdCultureCollection()
    {
    }
}

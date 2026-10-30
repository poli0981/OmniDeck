using OmniDeck.Core.Search;

namespace OmniDeck.Tests.Search;

public sealed class OdFuzzyMatcherTests
{
    [Theory]
    [InlineData("clk", "Clock")]
    [InlineData("CALC", "calculator")]
    [InlineData("wt", "World time")]
    [InlineData("", "anything")]
    public void Matches_CharactersInOrder_IgnoringCase(string query, string text)
    {
        Assert.NotNull(OdFuzzyMatcher.Score(query, text));
    }

    [Theory]
    [InlineData("kc", "Clock")]
    [InlineData("timers", "Timer")]
    [InlineData("x", "")]
    public void Rejects_MissingOrReorderedCharacters(string query, string text)
    {
        Assert.Null(OdFuzzyMatcher.Score(query, text));
    }

    [Theory]
    [InlineData("cal", "Calculator", "Local time")]
    [InlineData("t", "World time", "Stocks")]
    [InlineData("ws", "WebSearch", "Wikis")]
    [InlineData("ab", "abx", "axb")]
    [InlineData("timer", "Timer", "Timer settings")]
    public void RanksStartsWordBreaksRunsAndShorterTextsFirst(string query, string better, string worse)
    {
        Assert.True(OdFuzzyMatcher.Score(query, better) > OdFuzzyMatcher.Score(query, worse));
    }

    [Fact]
    public void Rank_DropsMisses_SortsByScore_AndKeepsTheOrderOfTies()
    {
        string[] items = ["Stocks", "Calendar", "Local time", "Calculator", "Media"];

        var ranked = OdFuzzyMatcher.Rank("  cal ", items, item => item);

        Assert.Equal(new[] { "Calendar", "Calculator", "Local time" }, ranked);
        Assert.Equal(items, OdFuzzyMatcher.Rank("", items, item => item));
    }
}

using OmniDeck.App.Shell;

namespace OmniDeck.Tests.Shell;

public sealed class OdCommandPaletteViewModelTests
{
    private static readonly OdPaletteEntry[] Entries =
    [
        Entry("Clock"),
        Entry("Timer"),
        Entry("Calculator"),
        Entry("Module library"),
        Entry("Quit OmniDeck"),
    ];

    [Fact]
    public void StartsWithEverything_AndTheFirstEntrySelected()
    {
        var palette = new OdCommandPaletteViewModel(Entries);

        Assert.Equal(Entries, palette.Results);
        Assert.Same(Entries[0], palette.Selected);
        Assert.Null(palette.Chosen);
    }

    [Fact]
    public void Query_FiltersAndRanks()
    {
        var palette = new OdCommandPaletteViewModel(Entries) { Query = "cl" };

        Assert.Equal(new[] { "Clock", "Calculator" }, palette.Results.Select(e => e.Title), StringComparer.Ordinal);
        Assert.Same(palette.Results[0], palette.Selected);

        palette.Query = "zzz";
        Assert.Empty(palette.Results);
        Assert.Null(palette.Selected);
    }

    [Fact]
    public void MoveSelection_StopsAtBothEnds()
    {
        var palette = new OdCommandPaletteViewModel(Entries);

        palette.MoveSelection(-1);
        Assert.Same(Entries[0], palette.Selected);
        palette.MoveSelection(2);
        Assert.Same(Entries[2], palette.Selected);
        palette.MoveSelection(10);
        Assert.Same(Entries[^1], palette.Selected);

        palette.Query = "zzz";
        palette.MoveSelection(1);
        Assert.Null(palette.Selected);
    }

    [Fact]
    public void Accept_ChoosesTheSelection_AndAnnouncesIt()
    {
        var palette = new OdCommandPaletteViewModel(Entries) { Query = "tim" };
        var accepted = 0;
        palette.Accepted += (_, _) => accepted++;

        palette.AcceptCommand.Execute(null);

        Assert.Same(Entries[1], palette.Chosen);
        Assert.Equal(1, accepted);
    }

    [Fact]
    public void Accept_WithNoResult_DoesNothing()
    {
        var palette = new OdCommandPaletteViewModel(Entries) { Query = "zzz" };
        var accepted = 0;
        palette.Accepted += (_, _) => accepted++;

        palette.AcceptCommand.Execute(null);

        Assert.Null(palette.Chosen);
        Assert.Equal(0, accepted);
    }

    private static OdPaletteEntry Entry(string title) => new(title, "Module", "Apps24", () => Task.CompletedTask);
}

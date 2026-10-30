using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using Microsoft.Extensions.Time.Testing;
using OmniDeck.App.Legal;
using OmniDeck.Core.Legal;
using OmniDeck.Core.Storage;
using OmniDeck.Tests.TestSupport;

namespace OmniDeck.Tests.Legal;

public sealed class OdLegalTests : IDisposable
{
    private readonly OdTempDirectory _temp = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero));
    private readonly OdAppMeta _meta;

    public OdLegalTests()
    {
        var database = new OdDatabase(_temp.Combine("omnideck.db"), pooling: false);
        database.Migrate(OdCoreMigrations.All);
        _meta = new OdAppMeta(database);
    }

    public void Dispose() => _temp.Dispose();

    [Theory]
    [InlineData("EULA.md")]
    [InlineData("DISCLAIMER.md")]
    [InlineData("PRIVACY.md")]
    public void TheShippedDocuments_CarryTheVersionTheGateAsksFor(string file)
    {
        var text = File.ReadAllText(Path.Combine(OdRepository.Root, "docs", "legal", file));

        Assert.Contains($"Version {OdLegal.Version} —", text, StringComparison.Ordinal);
        Assert.Contains(OdLegal.Documents, document => string.Equals(document.FileName, file, StringComparison.Ordinal));
    }

    [Fact]
    public void Gate_RecordsTheVersionAndTime_AndAsksAgainForANewVersion()
    {
        var gate = new OdLegalGate(_meta, _time);
        Assert.False(gate.IsAcknowledged);
        Assert.Null(gate.AcknowledgedAt);

        gate.Acknowledge();

        Assert.True(gate.IsAcknowledged);
        Assert.Equal(_time.GetUtcNow(), gate.AcknowledgedAt);
        _meta.Set(OdAppMetaKeys.LegalAcknowledgedVersion, "1.0");
        Assert.False(gate.IsAcknowledged);
    }

    [Fact]
    public void ViewModel_ShowsEveryDocument_AndContinuesOnlyOnceRead()
    {
        File.WriteAllText(_temp.Combine("EULA.md"), "# EULA");
        var gate = new OdLegalGate(_meta, _time);
        var legal = new OdLegalViewModel(gate, _temp.Path);
        var finished = 0;
        legal.Finished += (_, _) => finished++;

        Assert.Equal(new[] { "License agreement", "Disclaimer", "Privacy policy", "Third-party notices" }, legal.Pages.Select(p => p.Title), StringComparer.Ordinal);
        Assert.Equal("# EULA", legal.Pages[0].Markdown);
        Assert.Contains("DISCLAIMER.md", legal.Pages[1].Markdown, StringComparison.Ordinal);
        Assert.True(legal.AsksForAcknowledgment);
        Assert.Equal("Quit", legal.QuitText);
        Assert.False(legal.ContinueCommand.CanExecute(null));

        legal.HasRead = true;
        legal.ContinueCommand.Execute(null);

        Assert.True(legal.Accepted);
        Assert.True(gate.IsAcknowledged);
        Assert.Equal(1, finished);
    }

    [Fact]
    public void ViewModel_QuitAndReadOnly()
    {
        var gate = new OdLegalGate(_meta, _time);
        var legal = new OdLegalViewModel(gate, _temp.Path);
        legal.QuitCommand.Execute(null);
        Assert.False(legal.Accepted);
        Assert.False(gate.IsAcknowledged);

        var reading = new OdLegalViewModel(gate, _temp.Path, readOnly: true);
        Assert.False(reading.AsksForAcknowledgment);
        Assert.Equal("Close", reading.QuitText);
    }

    [Fact]
    public void Markdown_BecomesAFlowDocument() => OdSta.Run(() =>
    {
        const string markdown = """
            # Title

            Some *emphasis*, **strength**, `code` and a [link](https://example.com/a) and <https://example.com/b>.
            A [relative link](docs/x.md) stays text.

            ## Section

            - one
            - two

            1. first

            | A | B |
            |---|---|
            | 1 | 2 |

            > quoted

                indented code

            ---
            """;
        var opened = new List<Uri>();

        var document = OdMarkdownRenderer.Render(markdown, opened.Add);

        var blocks = document.Blocks.ToList();
        Assert.IsType<Paragraph>(blocks[0]);
        Assert.IsType<List>(blocks[3]);
        Assert.Equal(System.Windows.TextMarkerStyle.Decimal, Assert.IsType<List>(blocks[4]).MarkerStyle);
        Assert.IsType<Table>(blocks[5]);
        Assert.IsType<Section>(blocks[6]);
        Assert.IsType<Paragraph>(blocks[7]);
        Assert.Equal(9, blocks.Count);
        var links = Descendants(document).OfType<Hyperlink>().OrderBy(l => l.NavigateUri.AbsoluteUri, StringComparer.Ordinal).ToList();
        Assert.Equal(new[] { "https://example.com/a", "https://example.com/b" }, links.Select(l => l.NavigateUri.AbsoluteUri), StringComparer.Ordinal);
        Assert.Contains(Descendants(document), inline => inline is Bold);
        Assert.Contains(Descendants(document), inline => inline is Italic);

        links[0].RaiseEvent(new RequestNavigateEventArgs(links[0].NavigateUri, null) { RoutedEvent = Hyperlink.RequestNavigateEvent });
        Assert.Equal(new[] { new Uri("https://example.com/a") }, opened);
        return Task.CompletedTask;
    });

    [Fact]
    public void Markdown_LinesUseThemeBrushes() => OdSta.Run(() =>
    {
        const string markdown = """
            > quoted

            ---

            | A |
            |---|
            | 1 |
            """;
        var document = OdMarkdownRenderer.Render(markdown, _ => { });
        var divider = new SolidColorBrush(Colors.Red);
        var quote = new SolidColorBrush(Colors.Blue);

        document.Resources["DividerStrokeColorDefaultBrush"] = divider;
        document.Resources["ControlStrongStrokeColorDefaultBrush"] = quote;

        var blocks = document.Blocks.ToList();
        Assert.Same(quote, Assert.IsType<Section>(blocks[0]).BorderBrush);
        Assert.Same(divider, Assert.IsType<Paragraph>(blocks[1]).BorderBrush);
        Assert.Same(divider, Assert.IsType<Table>(blocks[2]).RowGroups[0].Rows[0].Cells[0].BorderBrush);
        return Task.CompletedTask;
    });

    [Fact]
    public void TheRealDocuments_Render() => OdSta.Run(() =>
    {
        foreach (var document in OdLegal.Documents)
        {
            var text = File.ReadAllText(Path.Combine(OdRepository.Root, "docs", "legal", document.FileName));
            var rendered = (FlowDocument)new OdMarkdownConverter().Convert(text, typeof(FlowDocument), null, System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(rendered.Blocks.Count > 5, document.FileName);
        }

        Assert.Same(System.Windows.Data.Binding.DoNothing, new OdMarkdownConverter().ConvertBack(null, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture));
        return Task.CompletedTask;
    });

    private static IEnumerable<TextElement> Descendants(FlowDocument document)
    {
        var pending = new Stack<object>(document.Blocks);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current is TextElement element)
            {
                yield return element;
            }

            IEnumerable<object> children = current switch
            {
                Paragraph paragraph => paragraph.Inlines,
                Span span => span.Inlines,
                List list => list.ListItems,
                ListItem item => item.Blocks,
                Section section => section.Blocks,
                Table table => table.RowGroups.SelectMany(group => group.Rows).SelectMany(row => row.Cells).SelectMany(cell => cell.Blocks),
                _ => [],
            };
            foreach (var child in children)
            {
                pending.Push(child);
            }
        }
    }
}

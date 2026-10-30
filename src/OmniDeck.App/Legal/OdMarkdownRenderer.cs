using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using OmniDeck.Core.Theming;
using MdBlock = Markdig.Syntax.Block;
using MdInline = Markdig.Syntax.Inlines.Inline;
using MdTable = Markdig.Extensions.Tables.Table;
using MdTableCell = Markdig.Extensions.Tables.TableCell;
using MdTableRow = Markdig.Extensions.Tables.TableRow;
using WpfBlock = System.Windows.Documents.Block;
using WpfInline = System.Windows.Documents.Inline;
using WpfTable = System.Windows.Documents.Table;

namespace OmniDeck.App.Legal;

/// <summary>
/// Turns the legal documents' Markdown into a <see cref="FlowDocument"/>. It covers what docs/legal
/// uses: headings, paragraphs, emphasis, inline code, links, lists, pipe tables, quotes, code blocks
/// and rules. Text sizes follow the <c>Od.*</c> font tokens; links go to the caller, which opens them.
/// </summary>
internal static class OdMarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePipeTables().Build();
    private static readonly FontFamily Monospace = new("Cascadia Mono, Consolas, Courier New");

    public static FlowDocument Render(string markdown, Action<Uri> openLink)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(openLink);
        // A FlowDocument defaults to a serif reading font and justified text; the app's own font reads better here.
        var document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = SystemFonts.MessageFontFamily,
            TextAlignment = TextAlignment.Left,
        };
        document.SetResourceReference(FlowDocument.FontSizeProperty, OdTokenKeys.FontBase);
        document.SetResourceReference(FlowDocument.ForegroundProperty, "TextFillColorPrimaryBrush");
        foreach (var block in Markdown.Parse(markdown, Pipeline))
        {
            if (RenderBlock(block, openLink) is { } rendered)
            {
                document.Blocks.Add(rendered);
            }
        }

        return document;
    }

    private static WpfBlock? RenderBlock(MdBlock block, Action<Uri> openLink) => block switch
    {
        HeadingBlock heading => Heading(heading, openLink),
        ParagraphBlock paragraph => Paragraph(paragraph.Inline, openLink),
        ListBlock list => List(list, openLink),
        MdTable table => Table(table, openLink),
        QuoteBlock quote => Quote(quote, openLink),
        CodeBlock code => Code(code),
        ThematicBreakBlock => new Paragraph { BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = Brushes.Gray },
        _ => null,
    };

    private static Paragraph Heading(HeadingBlock heading, Action<Uri> openLink)
    {
        var paragraph = Paragraph(heading.Inline, openLink);
        paragraph.FontWeight = FontWeights.SemiBold;
        paragraph.Margin = new Thickness(0, heading.Level == 1 ? 0 : 12, 0, 4);
        paragraph.SetResourceReference(
            TextElement.FontSizeProperty,
            heading.Level switch
            {
                1 => OdTokenKeys.FontH1,
                2 => OdTokenKeys.FontH2,
                _ => OdTokenKeys.FontBase,
            });
        return paragraph;
    }

    private static Paragraph Paragraph(ContainerInline? inlines, Action<Uri> openLink)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 8) };
        AddInlines(paragraph.Inlines, inlines, openLink);
        return paragraph;
    }

    private static List List(ListBlock list, Action<Uri> openLink)
    {
        var rendered = new List
        {
            MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
            Margin = new Thickness(0, 0, 0, 8),
        };
        foreach (var item in list.OfType<ListItemBlock>())
        {
            var listItem = new ListItem();
            foreach (var child in item)
            {
                if (RenderBlock(child, openLink) is { } block)
                {
                    block.Margin = new Thickness(0);
                    listItem.Blocks.Add(block);
                }
            }

            rendered.ListItems.Add(listItem);
        }

        return rendered;
    }

    private static WpfTable Table(MdTable table, Action<Uri> openLink)
    {
        var rendered = new WpfTable { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 8) };
        var rows = new TableRowGroup();
        foreach (var row in table.OfType<MdTableRow>())
        {
            var renderedRow = new TableRow();
            foreach (var cell in row.OfType<MdTableCell>())
            {
                var renderedCell = new TableCell { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(4, 2, 8, 2) };
                foreach (var block in cell)
                {
                    if (RenderBlock(block, openLink) is { } content)
                    {
                        content.Margin = new Thickness(0);
                        renderedCell.Blocks.Add(content);
                    }
                }

                if (row.IsHeader)
                {
                    renderedCell.FontWeight = FontWeights.SemiBold;
                }

                renderedRow.Cells.Add(renderedCell);
            }

            rows.Rows.Add(renderedRow);
        }

        rendered.RowGroups.Add(rows);
        return rendered;
    }

    private static Section Quote(QuoteBlock quote, Action<Uri> openLink)
    {
        var section = new Section { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(8, 0, 0, 0) };
        foreach (var child in quote)
        {
            if (RenderBlock(child, openLink) is { } block)
            {
                section.Blocks.Add(block);
            }
        }

        return section;
    }

    private static Paragraph Code(CodeBlock code) =>
        new(new Run(code.Lines.ToString())) { FontFamily = Monospace, Margin = new Thickness(0, 0, 0, 8) };

    private static void AddInlines(InlineCollection target, ContainerInline? container, Action<Uri> openLink)
    {
        for (var inline = container?.FirstChild; inline is not null; inline = inline.NextSibling)
        {
            if (RenderInline(inline, openLink) is { } rendered)
            {
                target.Add(rendered);
            }
        }
    }

    private static WpfInline? RenderInline(MdInline inline, Action<Uri> openLink)
    {
        switch (inline)
        {
            case LiteralInline literal:
                return new Run(literal.Content.ToString());
            case CodeInline code:
                return new Run(code.Content) { FontFamily = Monospace };
            case LineBreakInline lineBreak:
                return lineBreak.IsHard ? new LineBreak() : new Run(" ");
            case LinkInline { IsImage: false } link:
                return Link(link.Url, link, openLink);
            case AutolinkInline autolink:
                return Link(autolink.Url, null, openLink, autolink.Url);
            case EmphasisInline emphasis:
                Span span = emphasis.DelimiterCount >= 2 ? new Bold() : new Italic();
                AddInlines(span.Inlines, emphasis, openLink);
                return span;
            case ContainerInline container:
                var group = new Span();
                AddInlines(group.Inlines, container, openLink);
                return group;
            default:
                return null;
        }
    }

    private static WpfInline Link(string? url, ContainerInline? content, Action<Uri> openLink, string? text = null)
    {
        var span = new Span();
        if (content is not null)
        {
            AddInlines(span.Inlines, content, openLink);
        }
        else
        {
            span.Inlines.Add(new Run(text ?? url ?? ""));
        }

        // Only absolute web and mail links open; relative links point into the source repository.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http" or "mailto"))
        {
            return span;
        }

        var hyperlink = new Hyperlink(span) { NavigateUri = uri };
        hyperlink.RequestNavigate += (_, e) =>
        {
            openLink(e.Uri);
            e.Handled = true;
        };
        return hyperlink;
    }
}

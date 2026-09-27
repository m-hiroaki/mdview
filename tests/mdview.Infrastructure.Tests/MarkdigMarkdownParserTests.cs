using mdview.Application.Models;
using mdview.Infrastructure.Markdown;

namespace mdview.Infrastructure.Tests;

public class MarkdigMarkdownParserTests
{
  [Fact]
  public void Parse_MapsGfmBlockTypesAndNestedLists()
  {
    const string markdown = """
      # Guide

      A paragraph.

      > A quote.

      3. First
      4. Second

      - Bullet
        - Nested

      - [x] Done
      - [ ] Todo

      ```csharp
      var value = 1;
      ```

      | Name | Count |
      | :--- | ----: |
      | A    | 1     |

      ---
      """;

    var document = new MarkdigMarkdownParser().Parse(markdown);

    Assert.Equal("guide", Assert.Single(document.Blocks.OfType<MarkdownHeadingBlock>()).Anchor);
    Assert.Single(document.Blocks.OfType<MarkdownParagraphBlock>());
    Assert.Single(document.Blocks.OfType<MarkdownQuoteBlock>());

    var lists = document.Blocks.OfType<MarkdownListBlock>().ToArray();
    var orderedList = Assert.Single(lists, list => list.IsOrdered);
    Assert.True(orderedList.IsOrdered);
    Assert.Equal(3, orderedList.Start);

    var unorderedLists = lists.Where(list => !list.IsOrdered).ToArray();
    Assert.NotEmpty(unorderedLists);
    Assert.Contains(unorderedLists.SelectMany(list => list.Items).First().Blocks, block => block is MarkdownListBlock);

    var taskInlines = unorderedLists.SelectMany(list => list.Items)
      .SelectMany(item => item.Blocks.OfType<MarkdownParagraphBlock>())
      .SelectMany(paragraph => paragraph.Inlines.OfType<MarkdownTaskListInline>())
      .ToArray();
    Assert.Equal([true, false], taskInlines.Select(task => task.IsChecked));

    var code = Assert.Single(document.Blocks.OfType<MarkdownCodeBlock>());
    Assert.Equal("csharp", code.Language);
    Assert.Contains("var value = 1;", code.Code);

    var table = Assert.Single(document.Blocks.OfType<MarkdownTableBlock>());
    Assert.Equal(2, table.Rows.Count);
    Assert.Equal(MarkdownTableAlignment.Left, table.Alignments[0]);
    Assert.Equal(MarkdownTableAlignment.Right, table.Alignments[1]);
    Assert.Equal("Name", GetPlainText(table.Rows[0].Cells[0].Inlines));

    Assert.Single(document.Blocks.OfType<MarkdownThematicBreakBlock>());
  }

  [Fact]
  public void Parse_MapsInlineFormattingLinksImagesAndAutomaticLinks()
  {
    const string markdown = "**bold** *italic* ~~removed~~ `code` [site](https://example.com \"title\") ![chart](images/chart.png) https://example.org";

    var paragraph = Assert.IsType<MarkdownParagraphBlock>(new MarkdigMarkdownParser().Parse(markdown).Blocks.Single());

    Assert.IsType<MarkdownStrongInline>(paragraph.Inlines[0]);
    Assert.IsType<MarkdownEmphasisInline>(paragraph.Inlines[2]);
    Assert.IsType<MarkdownStrikethroughInline>(paragraph.Inlines[4]);
    Assert.Equal("code", Assert.IsType<MarkdownCodeInline>(paragraph.Inlines[6]).Code);

    var link = Assert.IsType<MarkdownLinkInline>(paragraph.Inlines[8]);
    Assert.Equal("https://example.com", link.Destination);
    Assert.Equal("title", link.Title);
    Assert.Equal("site", GetPlainText(link.Inlines));

    var image = Assert.IsType<MarkdownImageInline>(paragraph.Inlines[10]);
    Assert.Equal("images/chart.png", image.Destination);
    Assert.Equal("chart", image.AltText);

    var automaticLink = Assert.Single(paragraph.Inlines.OfType<MarkdownLinkInline>(),
      candidate => candidate.Destination == "https://example.org");
    Assert.Equal("https://example.org", GetPlainText(automaticLink.Inlines));
  }

  [Fact]
  public void Parse_CreatesUniqueGitHubStyleHeadingAnchors()
  {
    const string markdown = "# Parsing Basics!\n# Parsing Basics!\n# **Parsing** Basics-1";

    var headings = new MarkdigMarkdownParser().Parse(markdown).Blocks
      .Cast<MarkdownHeadingBlock>()
      .Select(heading => heading.Anchor)
      .ToArray();

    Assert.Equal(["parsing-basics", "parsing-basics-1", "parsing-basics-1-1"], headings);
  }

  [Fact]
  public void Parse_EmptyInputReturnsNoBlocks()
  {
    Assert.Empty(new MarkdigMarkdownParser().Parse(string.Empty).Blocks);
  }

  private static string GetPlainText(IEnumerable<MarkdownInline> inlines) => string.Concat(inlines.Select(inline => inline switch
  {
    MarkdownTextInline text => text.Text,
    MarkdownCodeInline code => code.Code,
    MarkdownEmphasisInline emphasis => GetPlainText(emphasis.Inlines),
    MarkdownStrongInline strong => GetPlainText(strong.Inlines),
    MarkdownStrikethroughInline strike => GetPlainText(strike.Inlines),
    MarkdownLinkInline link => GetPlainText(link.Inlines),
    MarkdownImageInline image => image.AltText,
    _ => string.Empty
  }));
}

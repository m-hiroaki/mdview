using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using mdview.Application.Abstractions;
using mdview.Application.Models;

namespace mdview.Infrastructure.Markdown;

public sealed class MarkdigMarkdownParser : IMarkdownParser
{
  private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
    .UsePipeTables()
    .UseTaskLists()
    .UseAutoLinks()
    .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
    .Build();

  private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
  private static readonly Regex NonAnchorCharacters = new(@"[^\p{L}\p{N}\p{M}_\- ]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

  public MarkdownDocumentModel Parse(string markdown)
  {
    ArgumentNullException.ThrowIfNull(markdown);

    var document = Markdig.Markdown.Parse(markdown, Pipeline);
    var anchors = new HashSet<string>(StringComparer.Ordinal);
    return new MarkdownDocumentModel(ConvertBlocks(document, anchors));
  }

  private static IReadOnlyList<MarkdownBlock> ConvertBlocks(ContainerBlock container, HashSet<string> anchors)
  {
    var blocks = new List<MarkdownBlock>(container.Count);

    foreach (var block in container)
    {
      var converted = ConvertBlock(block, anchors);
      if (converted is not null)
      {
        blocks.Add(converted);
      }
    }

    return blocks;
  }

  private static MarkdownBlock? ConvertBlock(Block block, HashSet<string> anchors)
  {
    switch (block)
    {
      case HeadingBlock heading:
      {
        var inlines = ConvertInlines(heading.Inline);
        var anchor = CreateUniqueAnchor(GetPlainText(inlines), anchors);
        return new MarkdownHeadingBlock(heading.Level, anchor, inlines);
      }
      case ParagraphBlock paragraph:
        return new MarkdownParagraphBlock(ConvertInlines(paragraph.Inline));
      case FencedCodeBlock fencedCode:
      {
        var info = fencedCode.Info?.ToString().Trim() ?? string.Empty;
        var language = info.Length == 0 ? null : info.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
        return new MarkdownCodeBlock(fencedCode.Lines.ToString(), language);
      }
      case CodeBlock code:
        return new MarkdownCodeBlock(code.Lines.ToString(), null);
      case ListBlock list:
      {
        var items = list.OfType<ListItemBlock>()
          .Select(item => new MarkdownListItem(ConvertBlocks(item, anchors)))
          .ToArray();
        var start = list.IsOrdered && int.TryParse(list.OrderedStart?.ToString(), out var orderedStart)
          ? orderedStart
          : 1;
        return new MarkdownListBlock(list.IsOrdered, start, items);
      }
      case QuoteBlock quote:
        return new MarkdownQuoteBlock(ConvertBlocks(quote, anchors));
      case Table table:
        return ConvertTable(table);
      case ThematicBreakBlock:
        return new MarkdownThematicBreakBlock();
      case HtmlBlock html:
        return new MarkdownHtmlBlock(html.Lines.ToString());
      default:
        return null;
    }
  }

  private static MarkdownTableBlock ConvertTable(Table table)
  {
    var rows = table.OfType<TableRow>()
      .Select(row => new MarkdownTableRow(
        row.IsHeader,
        row.OfType<TableCell>().Select(ConvertTableCell).ToArray()))
      .ToArray();

    var alignments = table.ColumnDefinitions is null
      ? Array.Empty<MarkdownTableAlignment>()
      : table.ColumnDefinitions.Select(column => column.Alignment switch
      {
        TableColumnAlign.Left => MarkdownTableAlignment.Left,
        TableColumnAlign.Center => MarkdownTableAlignment.Center,
        TableColumnAlign.Right => MarkdownTableAlignment.Right,
        _ => MarkdownTableAlignment.None
      }).ToArray();

    return new MarkdownTableBlock(rows, alignments);
  }

  private static MarkdownTableCell ConvertTableCell(TableCell cell)
  {
    var inlines = cell.OfType<ParagraphBlock>().SelectMany(paragraph => ConvertInlines(paragraph.Inline)).ToArray();
    return new MarkdownTableCell(inlines);
  }

  private static IReadOnlyList<MarkdownInline> ConvertInlines(ContainerInline? container)
  {
    var inlines = new List<MarkdownInline>();

    for (var inline = container?.FirstChild; inline is not null; inline = inline.NextSibling)
    {
      switch (inline)
      {
        case LiteralInline literal:
          inlines.Add(new MarkdownTextInline(literal.Content.ToString()));
          break;
        case CodeInline code:
          inlines.Add(new MarkdownCodeInline(code.Content.ToString()));
          break;
        case EmphasisInline emphasis:
          inlines.Add(ConvertEmphasis(emphasis));
          break;
        case LinkInline link when link.IsImage:
        {
          var children = ConvertInlines(link);
          inlines.Add(new MarkdownImageInline(link.Url ?? string.Empty, link.Title, GetPlainText(children)));
          break;
        }
        case LinkInline link:
          inlines.Add(new MarkdownLinkInline(link.Url ?? string.Empty, link.Title, ConvertInlines(link)));
          break;
        case LineBreakInline lineBreak:
          inlines.Add(new MarkdownBreakInline(lineBreak.IsHard));
          break;
        case TaskList taskList:
          inlines.Add(new MarkdownTaskListInline(taskList.Checked));
          break;
        case HtmlEntityInline entity:
          inlines.Add(new MarkdownTextInline(entity.Transcoded.ToString()));
          break;
        case HtmlInline html:
          inlines.Add(new MarkdownTextInline(html.Tag.ToString()));
          break;
      }
    }

    return inlines;
  }

  private static MarkdownInline ConvertEmphasis(EmphasisInline emphasis)
  {
    var content = ConvertInlines(emphasis);

    if (emphasis.DelimiterChar == '~')
    {
      return new MarkdownStrikethroughInline(content);
    }

    if (emphasis.DelimiterCount >= 3)
    {
      return new MarkdownEmphasisInline([new MarkdownStrongInline(content)]);
    }

    return emphasis.DelimiterCount == 2
      ? new MarkdownStrongInline(content)
      : new MarkdownEmphasisInline(content);
  }

  private static string GetPlainText(IEnumerable<MarkdownInline> inlines)
  {
    var text = new StringBuilder();

    foreach (var inline in inlines)
    {
      switch (inline)
      {
        case MarkdownTextInline literal:
          text.Append(literal.Text);
          break;
        case MarkdownCodeInline code:
          text.Append(code.Code);
          break;
        case MarkdownEmphasisInline emphasis:
          text.Append(GetPlainText(emphasis.Inlines));
          break;
        case MarkdownStrongInline strong:
          text.Append(GetPlainText(strong.Inlines));
          break;
        case MarkdownStrikethroughInline strikethrough:
          text.Append(GetPlainText(strikethrough.Inlines));
          break;
        case MarkdownLinkInline link:
          text.Append(GetPlainText(link.Inlines));
          break;
        case MarkdownImageInline image:
          text.Append(image.AltText);
          break;
        case MarkdownBreakInline:
          text.Append(' ');
          break;
      }
    }

    return text.ToString();
  }

  private static string CreateUniqueAnchor(string headingText, HashSet<string> anchors)
  {
    var normalized = Whitespace.Replace(headingText.ToLowerInvariant().Trim(), " ");
    var baseAnchor = NonAnchorCharacters.Replace(normalized, string.Empty).Replace(' ', '-');
    var candidate = baseAnchor;
    var suffix = 0;

    while (!anchors.Add(candidate))
    {
      candidate = $"{baseAnchor}-{++suffix}";
    }

    return candidate;
  }
}

namespace mdview.Application.Models;

public sealed record MarkdownDocumentModel(IReadOnlyList<MarkdownBlock> Blocks);

public abstract record MarkdownBlock;

public sealed record MarkdownHeadingBlock(int Level, string Anchor, IReadOnlyList<MarkdownInline> Inlines) : MarkdownBlock;

public sealed record MarkdownParagraphBlock(IReadOnlyList<MarkdownInline> Inlines) : MarkdownBlock;

public sealed record MarkdownCodeBlock(string Code, string? Language) : MarkdownBlock;

public sealed record MarkdownListBlock(bool IsOrdered, int Start, IReadOnlyList<MarkdownListItem> Items) : MarkdownBlock;

public sealed record MarkdownListItem(IReadOnlyList<MarkdownBlock> Blocks);

public sealed record MarkdownQuoteBlock(IReadOnlyList<MarkdownBlock> Blocks) : MarkdownBlock;

public sealed record MarkdownTableBlock(IReadOnlyList<MarkdownTableRow> Rows, IReadOnlyList<MarkdownTableAlignment> Alignments) : MarkdownBlock;

public sealed record MarkdownTableRow(bool IsHeader, IReadOnlyList<MarkdownTableCell> Cells);

public sealed record MarkdownTableCell(IReadOnlyList<MarkdownInline> Inlines);

public enum MarkdownTableAlignment
{
  None,
  Left,
  Center,
  Right
}

public sealed record MarkdownThematicBreakBlock : MarkdownBlock;

public sealed record MarkdownHtmlBlock(string Text) : MarkdownBlock;

public abstract record MarkdownInline;

public sealed record MarkdownTextInline(string Text) : MarkdownInline;

public sealed record MarkdownEmphasisInline(IReadOnlyList<MarkdownInline> Inlines) : MarkdownInline;

public sealed record MarkdownStrongInline(IReadOnlyList<MarkdownInline> Inlines) : MarkdownInline;

public sealed record MarkdownStrikethroughInline(IReadOnlyList<MarkdownInline> Inlines) : MarkdownInline;

public sealed record MarkdownCodeInline(string Code) : MarkdownInline;

public sealed record MarkdownLinkInline(string Destination, string? Title, IReadOnlyList<MarkdownInline> Inlines) : MarkdownInline;

public sealed record MarkdownImageInline(string Destination, string? Title, string AltText) : MarkdownInline;

public sealed record MarkdownBreakInline(bool IsHard) : MarkdownInline;

public sealed record MarkdownTaskListInline(bool IsChecked) : MarkdownInline;

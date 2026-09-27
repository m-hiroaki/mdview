using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using mdview.Application.Models;

namespace mdview.Presentation.Views;

internal static class MarkdownBlockRenderer
{
  private static readonly IBrush SubtleBorder = new SolidColorBrush(Color.Parse("#454545"));
  private static readonly IBrush LinkForeground = new SolidColorBrush(Color.Parse("#77BDFB"));
  private static readonly IBrush InlineCodeForeground = new SolidColorBrush(Color.Parse("#E6B673"));

  public static Control Render(MarkdownBlock block) => block switch
  {
    MarkdownHeadingBlock heading => RenderHeading(heading),
    MarkdownParagraphBlock paragraph => RenderParagraph(paragraph),
    MarkdownCodeBlock code => new MarkdownCodeBlockView(code),
    MarkdownListBlock list => RenderList(list),
    MarkdownQuoteBlock quote => RenderQuote(quote),
    MarkdownTableBlock table => RenderTable(table),
    MarkdownThematicBreakBlock => new Border
    {
      Height = 1,
      Background = SubtleBorder,
      Margin = new Thickness(0, 8)
    },
    MarkdownHtmlBlock html => RenderText(html.Text, new FontFamily("monospace")),
    _ => RenderText(string.Empty)
  };

  private static Control RenderHeading(MarkdownHeadingBlock heading)
  {
    var fontSize = heading.Level switch
    {
      1 => 32,
      2 => 28,
      3 => 24,
      4 => 21,
      5 => 18,
      _ => 16
    };

    return RenderInlineText(heading.Inlines, fontSize, FontWeight.Bold, new Thickness(0, 14, 0, 8));
  }

  private static Control RenderParagraph(MarkdownParagraphBlock paragraph) =>
    RenderInlineText(paragraph.Inlines, 16, FontWeight.Normal, new Thickness(0, 4, 0, 10));

  private static Control RenderList(MarkdownListBlock list)
  {
    var panel = new StackPanel
    {
      Spacing = 5,
      Margin = new Thickness(24, 4, 0, 10)
    };
    var orderedNumber = list.Start;

    foreach (var item in list.Items)
    {
      var isTaskItem = item.Blocks.OfType<MarkdownParagraphBlock>()
        .SelectMany(paragraph => paragraph.Inlines)
        .Any(inline => inline is MarkdownTaskListInline);
      var marker = isTaskItem ? string.Empty : list.IsOrdered ? $"{orderedNumber++}." : "•";
      var row = new Grid
      {
        ColumnDefinitions = new ColumnDefinitions("Auto,*")
      };
      row.Children.Add(new TextBlock
      {
        Text = marker,
        Margin = new Thickness(0, 1, 10, 0),
        VerticalAlignment = VerticalAlignment.Top
      });

      var contents = new StackPanel { Spacing = 5 };
      foreach (var child in item.Blocks)
      {
        contents.Children.Add(Render(child));
      }

      Grid.SetColumn(contents, 1);
      row.Children.Add(contents);
      panel.Children.Add(row);
    }

    return panel;
  }

  private static Control RenderQuote(MarkdownQuoteBlock quote)
  {
    var contents = new StackPanel { Spacing = 4 };
    foreach (var block in quote.Blocks)
    {
      contents.Children.Add(Render(block));
    }

    return new Border
    {
      BorderBrush = SubtleBorder,
      BorderThickness = new Thickness(3, 0, 0, 0),
      Padding = new Thickness(14, 4),
      Margin = new Thickness(0, 6, 0, 12),
      Child = contents
    };
  }

  private static Control RenderTable(MarkdownTableBlock table)
  {
    var columnCount = table.Rows.Count == 0 ? 0 : table.Rows.Max(row => row.Cells.Count);
    var grid = new Grid { Margin = new Thickness(0, 6, 0, 12) };

    for (var column = 0; column < columnCount; column++)
    {
      grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
    }

    for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
    {
      var row = table.Rows[rowIndex];
      grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

      for (var columnIndex = 0; columnIndex < row.Cells.Count; columnIndex++)
      {
        var cell = row.Cells[columnIndex];
        var text = CreateInlineText(cell.Inlines);
        text.FontWeight = row.IsHeader ? FontWeight.Bold : FontWeight.Normal;
        text.TextAlignment = GetTextAlignment(table.Alignments, columnIndex);

        var border = new Border
        {
          BorderBrush = SubtleBorder,
          BorderThickness = new Thickness(0, 0, 1, 1),
          Padding = new Thickness(8, 6),
          Child = text
        };
        Grid.SetRow(border, rowIndex);
        Grid.SetColumn(border, columnIndex);
        grid.Children.Add(border);
      }
    }

    return grid;
  }

  private static TextAlignment GetTextAlignment(IReadOnlyList<MarkdownTableAlignment> alignments, int columnIndex)
  {
    if (columnIndex >= alignments.Count)
    {
      return TextAlignment.Left;
    }

    return alignments[columnIndex] switch
    {
      MarkdownTableAlignment.Center => TextAlignment.Center,
      MarkdownTableAlignment.Right => TextAlignment.Right,
      _ => TextAlignment.Left
    };
  }

  private static TextBlock RenderInlineText(
    IReadOnlyList<MarkdownInline> inlines,
    double fontSize,
    FontWeight fontWeight,
    Thickness margin)
  {
    var text = CreateInlineText(inlines);
    text.FontSize = fontSize;
    text.FontWeight = fontWeight;
    text.Margin = margin;
    return text;
  }

  private static TextBlock RenderText(string value, FontFamily? fontFamily = null)
  {
    var text = new TextBlock
    {
      Text = value,
      TextWrapping = TextWrapping.Wrap,
      Margin = new Thickness(0, 4, 0, 10)
    };
    if (fontFamily is not null)
    {
      text.FontFamily = fontFamily;
    }

    return text;
  }

  private static TextBlock CreateInlineText(IEnumerable<MarkdownInline> inlines)
  {
    var collection = new InlineCollection();
    foreach (var inline in inlines)
    {
      collection.Add(RenderInline(inline));
    }

    return new TextBlock { TextWrapping = TextWrapping.Wrap, Inlines = collection };
  }

  private static Inline RenderInline(MarkdownInline inline) => inline switch
  {
    MarkdownTextInline literal => new Run(literal.Text),
    MarkdownCodeInline code => new Run(code.Code)
    {
      FontFamily = new FontFamily("monospace"),
      Foreground = InlineCodeForeground
    },
    MarkdownEmphasisInline emphasis => RenderSpan(emphasis.Inlines, span => span.FontStyle = FontStyle.Italic),
    MarkdownStrongInline strong => RenderSpan(strong.Inlines, span => span.FontWeight = FontWeight.Bold),
    MarkdownStrikethroughInline strike => RenderSpan(strike.Inlines, span => span.TextDecorations = TextDecorations.Strikethrough),
    MarkdownLinkInline link => RenderSpan(link.Inlines, span =>
    {
      span.Foreground = LinkForeground;
      span.TextDecorations = TextDecorations.Underline;
    }),
    MarkdownImageInline image => RenderSpan([new MarkdownTextInline(string.IsNullOrEmpty(image.AltText) ? "image" : image.AltText)],
      span => span.FontStyle = FontStyle.Italic),
    MarkdownBreakInline => new LineBreak(),
    MarkdownTaskListInline task => new Run(task.IsChecked ? "☑ " : "☐ "),
    _ => new Run(string.Empty)
  };

  private static Span RenderSpan(IEnumerable<MarkdownInline> inlines, Action<Span> style)
  {
    var span = new Span();
    style(span);
    var collection = new InlineCollection();
    foreach (var inline in inlines)
    {
      collection.Add(RenderInline(inline));
    }

    span.Inlines = collection;
    return span;
  }
}

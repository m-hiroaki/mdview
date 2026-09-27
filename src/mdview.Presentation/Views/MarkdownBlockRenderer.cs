using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using mdview.Application.Models;
using System.Text;

namespace mdview.Presentation.Views;

internal static class MarkdownBlockRenderer
{
  private static readonly IBrush SubtleBorder = new SolidColorBrush(Color.Parse("#454545"));
  private static readonly IBrush LinkForeground = new SolidColorBrush(Color.Parse("#77BDFB"));
  private static readonly IBrush InlineCodeForeground = new SolidColorBrush(Color.Parse("#E6B673"));
  private static readonly IBrush SearchHighlight = new SolidColorBrush(Color.Parse("#806B2A"));

  public static Control Render(MarkdownBlock block, string? currentMarkdownPath = null, string? searchQuery = null) => block switch
  {
    MarkdownHeadingBlock heading => RenderHeading(heading, currentMarkdownPath, searchQuery),
    MarkdownParagraphBlock paragraph => RenderParagraph(paragraph, currentMarkdownPath, searchQuery),
    MarkdownCodeBlock code => new MarkdownCodeBlockView(code),
    MarkdownListBlock list => RenderList(list, currentMarkdownPath, searchQuery),
    MarkdownQuoteBlock quote => RenderQuote(quote, currentMarkdownPath, searchQuery),
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

  private static Control RenderHeading(MarkdownHeadingBlock heading, string? currentMarkdownPath, string? searchQuery)
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

    var panel = new WrapPanel
    {
      Orientation = Orientation.Horizontal,
      Margin = new Thickness(0, 14, 0, 8),
      Tag = heading.Anchor
    };

    foreach (var inline in heading.Inlines)
    {
      panel.Children.Add(CreateInlineControl(inline, currentMarkdownPath, fontSize, FontWeight.Bold, searchQuery));
    }

    return panel;
  }

  private static Control RenderParagraph(MarkdownParagraphBlock paragraph, string? currentMarkdownPath, string? searchQuery)
  {
    var panel = new WrapPanel
    {
      Orientation = Orientation.Horizontal,
      Margin = new Thickness(0, 4, 0, 10)
    };

    foreach (var inline in paragraph.Inlines)
    {
      panel.Children.Add(CreateInlineControl(inline, currentMarkdownPath, 16, FontWeight.Normal, searchQuery));
    }

    return panel;
  }

  private static Control RenderList(MarkdownListBlock list, string? currentMarkdownPath, string? searchQuery)
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
        contents.Children.Add(Render(child, currentMarkdownPath, searchQuery));
      }

      Grid.SetColumn(contents, 1);
      row.Children.Add(contents);
      panel.Children.Add(row);
    }

    return panel;
  }

  private static Control RenderQuote(MarkdownQuoteBlock quote, string? currentMarkdownPath, string? searchQuery)
  {
    var contents = new StackPanel { Spacing = 4 };
    foreach (var block in quote.Blocks)
    {
      contents.Children.Add(Render(block, currentMarkdownPath, searchQuery));
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
        var text = BuildInlineTextBlock(cell.Inlines, 16, row.IsHeader ? FontWeight.Bold : FontWeight.Normal);
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

  private static Control CreateInlineControl(MarkdownInline inline, string? currentMarkdownPath, double fontSize, FontWeight fontWeight, string? searchQuery)
  {
    return inline switch
    {
      MarkdownTextInline literal => CreateHighlightedText(literal.Text, fontSize, fontWeight, searchQuery),
      MarkdownCodeInline code => new Border
      {
        Padding = new Thickness(4, 2),
        Margin = new Thickness(0, 0, 4, 0),
        Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
        Child = new TextBlock
        {
          Text = code.Code,
          FontFamily = new FontFamily("monospace"),
          FontSize = fontSize - 2,
          Foreground = InlineCodeForeground
        }
      },
      MarkdownEmphasisInline emphasis => new TextBlock
      {
        Text = GetPlainText(emphasis.Inlines),
        FontSize = fontSize,
        FontWeight = fontWeight,
        FontStyle = FontStyle.Italic,
        TextWrapping = TextWrapping.Wrap
      },
      MarkdownStrongInline strong => new TextBlock
      {
        Text = GetPlainText(strong.Inlines),
        FontSize = fontSize,
        FontWeight = FontWeight.Bold,
        TextWrapping = TextWrapping.Wrap
      },
      MarkdownStrikethroughInline strike => new TextBlock
      {
        Text = GetPlainText(strike.Inlines),
        FontSize = fontSize,
        FontWeight = fontWeight,
        TextDecorations = TextDecorations.Strikethrough,
        TextWrapping = TextWrapping.Wrap
      },
      MarkdownLinkInline link => CreateLinkButton(link, currentMarkdownPath, fontSize, fontWeight, searchQuery),
      MarkdownImageInline image => CreateImageControl(image, currentMarkdownPath, fontSize),
      MarkdownBreakInline => new TextBlock { Text = " ", FontSize = fontSize },
      MarkdownTaskListInline task => new TextBlock
      {
        Text = task.IsChecked ? "☑ " : "☐ ",
        FontSize = fontSize,
        FontWeight = fontWeight,
        Foreground = LinkForeground
      },
      _ => new TextBlock { Text = string.Empty }
    };
  }

  private static TextBlock CreateHighlightedText(string text, double fontSize, FontWeight fontWeight, string? searchQuery)
  {
    var block = new TextBlock
    {
      FontSize = fontSize,
      FontWeight = fontWeight,
      TextWrapping = TextWrapping.Wrap
    };

    if (string.IsNullOrEmpty(searchQuery))
    {
      block.Text = text;
      return block;
    }

    var inlines = new InlineCollection();
    var offset = 0;
    while (offset < text.Length)
    {
      var match = text.IndexOf(searchQuery, offset, StringComparison.CurrentCultureIgnoreCase);
      if (match < 0)
      {
        inlines.Add(new Run(text[offset..]));
        break;
      }

      if (match > offset)
      {
        inlines.Add(new Run(text[offset..match]));
      }

      inlines.Add(new Run(text.Substring(match, searchQuery.Length)) { Background = SearchHighlight });
      offset = match + searchQuery.Length;
    }

    block.Inlines = inlines;
    return block;
  }

  private static TextBlock BuildInlineTextBlock(IReadOnlyList<MarkdownInline> inlines, double fontSize, FontWeight fontWeight)
  {
    var block = new TextBlock
    {
      FontSize = fontSize,
      FontWeight = fontWeight,
      TextWrapping = TextWrapping.Wrap
    };

    var collection = new InlineCollection();
    foreach (var inline in inlines)
    {
      collection.Add(CreateInlineSpan(inline, null, fontSize, fontWeight));
    }

    block.Inlines = collection;
    return block;
  }

  private static Inline CreateInlineSpan(MarkdownInline inline, string? currentMarkdownPath, double fontSize, FontWeight fontWeight)
  {
    return inline switch
    {
      MarkdownTextInline text => new Run(text.Text),
      MarkdownCodeInline code => new Run(code.Code)
      {
        FontFamily = new FontFamily("monospace"),
        Foreground = InlineCodeForeground
      },
      MarkdownEmphasisInline emphasis => new Span
      {
        FontStyle = FontStyle.Italic,
        Inlines = BuildInlineCollection(emphasis.Inlines, fontSize, fontWeight)
      },
      MarkdownStrongInline strong => new Span
      {
        FontWeight = FontWeight.Bold,
        Inlines = BuildInlineCollection(strong.Inlines, fontSize, fontWeight)
      },
      MarkdownStrikethroughInline strike => new Span
      {
        TextDecorations = TextDecorations.Strikethrough,
        Inlines = BuildInlineCollection(strike.Inlines, fontSize, fontWeight)
      },
      MarkdownLinkInline link => new Run(GetPlainText(link.Inlines)),
      MarkdownImageInline image => new Run(string.IsNullOrWhiteSpace(image.AltText) ? "image" : image.AltText),
      MarkdownBreakInline => new LineBreak(),
      MarkdownTaskListInline task => new Run(task.IsChecked ? "☑ " : "☐ "),
      _ => new Run(string.Empty)
    };
  }

  private static InlineCollection BuildInlineCollection(IReadOnlyList<MarkdownInline> inlines, double fontSize, FontWeight fontWeight)
  {
    var collection = new InlineCollection();
    foreach (var inline in inlines)
    {
      collection.Add(CreateInlineSpan(inline, null, fontSize, fontWeight));
    }

    return collection;
  }

  private static Button CreateLinkButton(MarkdownLinkInline link, string? currentMarkdownPath, double fontSize, FontWeight fontWeight, string? searchQuery)
  {
    var button = new Button
    {
      Content = new TextBlock
      {
        FontSize = fontSize,
        FontWeight = fontWeight,
        Foreground = LinkForeground,
        TextDecorations = TextDecorations.Underline
      },
      Background = Brushes.Transparent,
      BorderThickness = new Thickness(0),
      Padding = new Thickness(0, 0, 2, 0),
      Cursor = new Cursor(StandardCursorType.Hand)
    };

    button.Content = CreateHighlightedText(GetPlainText(link.Inlines), fontSize, fontWeight, searchQuery);
    ((TextBlock)button.Content).Foreground = LinkForeground;
    ((TextBlock)button.Content).TextDecorations = TextDecorations.Underline;

    button.Click += (_, _) => MarkdownLinkHandler.Open(currentMarkdownPath, link.Destination);
    return button;
  }

  private static Control CreateImageControl(MarkdownImageInline image, string? currentMarkdownPath, double fontSize)
  {
    var path = MarkdownLinkHandler.ResolveImagePath(currentMarkdownPath, image.Destination);
    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
    {
      return CreateUnavailableImageText(image, fontSize);
    }

    try
    {
      return new Image
      {
        Source = new Bitmap(path),
        MaxHeight = 600,
        Stretch = Stretch.Uniform,
        HorizontalAlignment = HorizontalAlignment.Left,
        Margin = new Thickness(0, 4, 0, 10)
      };
    }
    catch (Exception)
    {
      return CreateUnavailableImageText(image, fontSize);
    }
  }

  private static TextBlock CreateUnavailableImageText(MarkdownImageInline image, double fontSize) => new()
  {
    Text = $"[Image unavailable: {image.AltText}]",
    FontSize = fontSize,
    FontStyle = FontStyle.Italic,
    Foreground = LinkForeground,
    TextWrapping = TextWrapping.Wrap
  };

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

  private static string GetPlainText(IEnumerable<MarkdownInline> inlines)
  {
    var builder = new StringBuilder();

    foreach (var inline in inlines)
    {
      switch (inline)
      {
        case MarkdownTextInline text:
          builder.Append(text.Text);
          break;
        case MarkdownCodeInline code:
          builder.Append(code.Code);
          break;
        case MarkdownEmphasisInline emphasis:
          builder.Append(GetPlainText(emphasis.Inlines));
          break;
        case MarkdownStrongInline strong:
          builder.Append(GetPlainText(strong.Inlines));
          break;
        case MarkdownStrikethroughInline strike:
          builder.Append(GetPlainText(strike.Inlines));
          break;
        case MarkdownLinkInline link:
          builder.Append(GetPlainText(link.Inlines));
          break;
        case MarkdownImageInline image:
          builder.Append(string.IsNullOrWhiteSpace(image.AltText) ? "image" : image.AltText);
          break;
        case MarkdownBreakInline:
          builder.Append(' ');
          break;
        case MarkdownTaskListInline task:
          builder.Append(task.IsChecked ? "☑ " : "☐ ");
          break;
      }
    }

    return builder.ToString();
  }
}

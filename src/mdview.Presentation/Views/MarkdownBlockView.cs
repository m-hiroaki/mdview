using Avalonia.Controls;
using mdview.Application.Models;

namespace mdview.Presentation.Views;

public sealed class MarkdownBlockView : ContentControl
{
  protected override void OnDataContextChanged(EventArgs e)
  {
    base.OnDataContextChanged(e);
    Content = DataContext is MarkdownBlock block ? MarkdownBlockRenderer.Render(block) : null;
  }
}

using Avalonia.Controls;
using mdview.Application.Models;
using mdview.Presentation.ViewModels;

namespace mdview.Presentation.Views;

public sealed class MarkdownBlockView : ContentControl
{
  protected override void OnDataContextChanged(EventArgs e)
  {
    base.OnDataContextChanged(e);
    UpdateContent();
  }

  private void UpdateContent()
  {
    if (DataContext is not MarkdownBlock block)
    {
      Content = null;
      return;
    }

    var currentMarkdownPath = VisualRoot is Window window && window.DataContext is MainWindowViewModel viewModel
      ? viewModel.ActiveTab?.FilePath
      : null;

    Content = MarkdownBlockRenderer.Render(block, currentMarkdownPath);
  }
}

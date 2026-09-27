using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using System.ComponentModel;
using mdview.Application.Models;
using mdview.Presentation.ViewModels;

namespace mdview.Presentation.Views;

public sealed class MarkdownBlockView : ContentControl
{
  private MainWindowViewModel? _viewModel;

  protected override void OnDataContextChanged(EventArgs e)
  {
    base.OnDataContextChanged(e);
    UpdateContent();
  }

  protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnAttachedToVisualTree(e);
    _viewModel = (VisualRoot as Window)?.DataContext as MainWindowViewModel;
    if (_viewModel is not null)
    {
      _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    UpdateContent();
  }

  protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
  {
    if (_viewModel is not null)
    {
      _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
      _viewModel = null;
    }

    base.OnDetachedFromVisualTree(e);
  }

  private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName == nameof(MainWindowViewModel.SearchQuery))
    {
      UpdateContent();
    }
  }

  private void UpdateContent()
  {
    if (DataContext is not MarkdownBlock block)
    {
      Content = null;
      return;
    }

    var viewModel = _viewModel ?? (VisualRoot as Window)?.DataContext as MainWindowViewModel;
    var currentMarkdownPath = viewModel?.ActiveTab?.FilePath;

    Content = MarkdownBlockRenderer.Render(block, currentMarkdownPath, viewModel?.SearchQuery);
  }
}

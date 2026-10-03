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
  private MarkdownBlock? _renderedBlock;
  private string? _renderedPath;
  private string? _renderedQuery;
  private mdview.Application.UseCases.MermaidDiagramService? _renderedDiagrams;

  protected override void OnDataContextChanged(EventArgs e)
  {
    base.OnDataContextChanged(e);
    UpdateContent();
  }

  protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnAttachedToVisualTree(e);
    _viewModel = TopLevel.GetTopLevel(this)?.DataContext as MainWindowViewModel;
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
    if (e.PropertyName == nameof(MainWindowViewModel.SearchQuery) && DataContext is not MarkdownMermaidBlock)
    {
      UpdateContent();
    }
  }

  private void UpdateContent()
  {
    if (DataContext is not MarkdownBlock block)
    {
      Content = null;
      _renderedBlock = null;
      return;
    }

    var viewModel = _viewModel ?? TopLevel.GetTopLevel(this)?.DataContext as MainWindowViewModel;
    var currentMarkdownPath = viewModel?.ActiveTab?.FilePath;
    var searchQuery = block is MarkdownMermaidBlock ? null : viewModel?.SearchQuery;
    var diagrams = viewModel?.Diagrams;
    if (ReferenceEquals(_renderedBlock, block) && _renderedPath == currentMarkdownPath &&
        _renderedQuery == searchQuery && ReferenceEquals(_renderedDiagrams, diagrams)) return;

    Content = MarkdownBlockRenderer.Render(block, currentMarkdownPath, searchQuery, diagrams);
    _renderedBlock = block;
    _renderedPath = currentMarkdownPath;
    _renderedQuery = searchQuery;
    _renderedDiagrams = diagrams;
  }
}

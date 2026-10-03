using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Svg.Skia;
using Avalonia.VisualTree;
using mdview.Application.Models;
using mdview.Application.UseCases;
using mdview.Presentation.ViewModels;

namespace mdview.Presentation.Views;

public sealed class MermaidDiagramView : Border
{
  private readonly MermaidDiagramViewModel? _viewModel;
  private readonly string _source;
  private CancellationTokenSource? _loading;
  private SvgSource? _svg;

  public MermaidDiagramView(MarkdownMermaidBlock block, MermaidDiagramService? service)
  {
    _source = block.Source;
    Margin = new Thickness(0, 6, 0, 12);
    if (service is null)
    {
      ShowError("Mermaid は現在 macOS のみ対応しています。");
      return;
    }
    _viewModel = new(service, block.Source);
    Child = new TextBlock { Text = _viewModel.Message };
  }

  protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnAttachedToVisualTree(e);
    if (_viewModel is null) { ShowError("Mermaid は現在 macOS のみ対応しています。"); return; }
    _loading = new();
    _viewModel.PropertyChanged += OnChanged;
    _ = _viewModel.LoadAsync(_loading.Token);
  }

  protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
  {
    _loading?.Cancel();
    _loading?.Dispose();
    _loading = null;
    if (_viewModel is not null) _viewModel.PropertyChanged -= OnChanged;
    // Virtualization detaches off-screen blocks. Preserve their measured height while
    // releasing the picture so the scroll extent does not shrink during reloading.
    var height = Child?.Bounds.Height ?? 0;
    Child = new Border { Height = height };
    _svg?.Dispose();
    _svg = null;
    base.OnDetachedFromVisualTree(e);
  }

  private async void OnChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
  {
    var token = _loading?.Token ?? new CancellationToken(true);
    if (_viewModel?.Document is { } document)
    {
      SvgSource? loaded = null;
      try
      {
        loaded = await Task.Run(() => SvgSource.LoadFromSvg(document.Svg, new global::Svg.Model.SvgParameters(
          null, null, null, new global::Svg.SvgDocumentLoadOptions
          {
            ExternalResources = global::Svg.SvgExternalResourcePolicy.Disabled
          })), token);
        if (token.IsCancellationRequested) { loaded?.Dispose(); return; }
        if (loaded?.Picture is null) throw new InvalidOperationException();
        _svg?.Dispose();
        _svg = loaded;
        // Svg loads its picture asynchronously. Explicit dimensions keep layout
        // stable even before the control has finished loading that picture.
        Child = new Viewbox
        {
          Stretch = Stretch.Uniform,
          StretchDirection = StretchDirection.DownOnly,
          IsHitTestVisible = false,
          Child = new Avalonia.Svg.Skia.Svg(new Uri("avares://mdview/"))
          {
            SvgSource = _svg, Width = document.Width, Height = document.Height
          }
        };
      }
      catch (OperationCanceledException) { loaded?.Dispose(); }
      catch (Exception)
      {
        loaded?.Dispose();
        if (token.IsCancellationRequested) return;
        _svg = null;
        ShowError("この図の SVG を表示できませんでした。");
      }
    }
    else if (_viewModel?.HasError == true) ShowError(_viewModel.Message);
  }

  private void ShowError(string message)
  {
    var panel = new StackPanel { Spacing = 6 };
    panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
    panel.Children.Add(new MarkdownCodeBlockView(new MarkdownCodeBlock(_source, null)));
    Child = panel;
  }
}

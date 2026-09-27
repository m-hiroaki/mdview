using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using mdview.Application.UseCases;
using mdview.Presentation.ViewModels;

namespace mdview.Presentation;

public static class MarkdownLinkHandler
{
  private static readonly MarkdownPathResolver PathResolver = new();

  public static string ResolveDocumentPath(string destination, string? currentMarkdownPath)
  {
    if (string.IsNullOrWhiteSpace(destination))
    {
      return string.Empty;
    }

    if (string.IsNullOrWhiteSpace(currentMarkdownPath))
    {
      return destination;
    }

    if (destination.StartsWith("#", StringComparison.Ordinal))
    {
      return destination;
    }

    if (Uri.TryCreate(destination, UriKind.Absolute, out var absoluteUri) &&
        (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
    {
      return destination;
    }

    return PathResolver.ResolveDocumentLink(currentMarkdownPath, destination);
  }

  public static void Open(string? currentMarkdownPath, string destination)
  {
    if (string.IsNullOrWhiteSpace(destination))
    {
      return;
    }

    if (Uri.TryCreate(destination, UriKind.Absolute, out var absoluteUri) && (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
    {
      OpenExternalUrl(destination);
      return;
    }

    if (string.IsNullOrWhiteSpace(currentMarkdownPath))
    {
      return;
    }

    if (destination.StartsWith("#", StringComparison.Ordinal))
    {
      ScrollToAnchor(destination[1..]);
      return;
    }

    var resolvedPath = ResolveDocumentPath(destination, currentMarkdownPath);
    OpenDocumentPath(resolvedPath);
  }

  public static string ResolveImagePath(string? currentMarkdownPath, string destination)
  {
    if (string.IsNullOrWhiteSpace(destination))
    {
      return string.Empty;
    }

    if (string.IsNullOrWhiteSpace(currentMarkdownPath))
    {
      return destination;
    }

    if (Uri.TryCreate(destination, UriKind.Absolute, out var absoluteUri) &&
        (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
    {
      return string.Empty;
    }

    return PathResolver.ResolveImagePath(currentMarkdownPath, destination);
  }

  public static void OpenExternalUrl(string url)
  {
    if (OperatingSystem.IsWindows())
    {
      Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
      return;
    }

    if (OperatingSystem.IsMacOS())
    {
      Process.Start("open", url);
      return;
    }

    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
  }

  private static void OpenDocumentPath(string resolvedPath)
  {
    var lifetime = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
    var mainWindow = lifetime?.MainWindow as MainWindow;
    var viewModel = mainWindow?.DataContext as MainWindowViewModel;

    if (viewModel is null)
    {
      return;
    }

    var filePath = resolvedPath.Contains('#') ? resolvedPath[..resolvedPath.IndexOf('#')] : resolvedPath;
    var anchor = resolvedPath.Contains('#') ? resolvedPath[(resolvedPath.IndexOf('#') + 1)..] : null;

    _ = viewModel.OpenFileAsync(filePath);

    if (!string.IsNullOrWhiteSpace(anchor))
    {
      mainWindow?.ScrollToAnchor(anchor);
    }
  }

  private static void ScrollToAnchor(string anchor)
  {
    var lifetime = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
    var mainWindow = lifetime?.MainWindow as MainWindow;
    mainWindow?.ScrollToAnchor(anchor);
  }
}

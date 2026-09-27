namespace mdview.Application.UseCases;

public sealed class MarkdownPathResolver
{
  public string ResolveDocumentLink(string currentMarkdownPath, string linkTarget)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(currentMarkdownPath);
    ArgumentNullException.ThrowIfNull(linkTarget);

    if (string.IsNullOrEmpty(linkTarget) || linkTarget.StartsWith("#", StringComparison.Ordinal))
    {
      return linkTarget;
    }

    if (Uri.TryCreate(linkTarget, UriKind.Absolute, out _))
    {
      return linkTarget;
    }

    var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(currentMarkdownPath)) ?? string.Empty;
    return Path.GetFullPath(Path.Combine(baseDirectory, linkTarget));
  }

  public string ResolveImagePath(string currentMarkdownPath, string imageTarget)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(currentMarkdownPath);
    ArgumentNullException.ThrowIfNull(imageTarget);

    if (Uri.TryCreate(imageTarget, UriKind.Absolute, out _))
    {
      return imageTarget;
    }

    var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(currentMarkdownPath)) ?? string.Empty;
    return Path.GetFullPath(Path.Combine(baseDirectory, imageTarget));
  }
}

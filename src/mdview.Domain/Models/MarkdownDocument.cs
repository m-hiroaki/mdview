namespace mdview.Domain.Models;

public sealed record MarkdownDocument
{
  public string FilePath { get; }
  public string Content { get; }

  public MarkdownDocument(string filePath, string content)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
    ArgumentNullException.ThrowIfNull(content);

    FilePath = filePath;
    Content = content;
  }
}

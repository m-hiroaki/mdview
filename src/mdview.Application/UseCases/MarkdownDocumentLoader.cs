using mdview.Application.Abstractions;
using mdview.Domain.Models;

namespace mdview.Application.UseCases;

public sealed class MarkdownDocumentLoader(IMarkdownFileReader fileReader)
{
  private readonly IMarkdownFileReader _fileReader = fileReader ?? throw new ArgumentNullException(nameof(fileReader));

  public async Task<MarkdownDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(path);

    var content = await _fileReader.ReadTextAsync(path, cancellationToken);
    return new MarkdownDocument(path, content);
  }
}

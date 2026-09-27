using System.Text;
using mdview.Application.Abstractions;

namespace mdview.Infrastructure.FileSystem;

public sealed class MarkdownFileReader : IMarkdownFileReader
{
  private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

  public Task<string> ReadTextAsync(string path, CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(path);
    return File.ReadAllTextAsync(path, Utf8, cancellationToken);
  }
}

using mdview.Application.Abstractions;
using mdview.Application.UseCases;

namespace mdview.Application.Tests;

public class MarkdownDocumentLoaderTests
{
  [Fact]
  public async Task LoadAsync_UsesFileReaderAndReturnsDocument()
  {
    var fileReader = new StubMarkdownFileReader("# Test");
    var loader = new MarkdownDocumentLoader(fileReader);
    var cancellationToken = TestContext.Current.CancellationToken;

    var document = await loader.LoadAsync("docs/readme.md", cancellationToken);

    Assert.Equal("docs/readme.md", fileReader.RequestedPath);
    Assert.Equal(cancellationToken, fileReader.RequestedCancellationToken);
    Assert.Equal("docs/readme.md", document.FilePath);
    Assert.Equal("# Test", document.Content);
  }

  [Fact]
  public async Task LoadAsync_RejectsEmptyPathBeforeReading()
  {
    var fileReader = new StubMarkdownFileReader("content");
    var loader = new MarkdownDocumentLoader(fileReader);

    await Assert.ThrowsAsync<ArgumentException>(() => loader.LoadAsync(" ", TestContext.Current.CancellationToken));
    Assert.Null(fileReader.RequestedPath);
  }

  private sealed class StubMarkdownFileReader(string content) : IMarkdownFileReader
  {
    public string? RequestedPath { get; private set; }
    public CancellationToken RequestedCancellationToken { get; private set; }

    public Task<string> ReadTextAsync(string path, CancellationToken cancellationToken = default)
    {
      RequestedPath = path;
      RequestedCancellationToken = cancellationToken;
      return Task.FromResult(content);
    }
  }
}

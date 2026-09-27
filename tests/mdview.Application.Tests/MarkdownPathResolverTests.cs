using mdview.Application.UseCases;

namespace mdview.Application.Tests;

public class MarkdownPathResolverTests
{
  [Fact]
  public void ResolveDocumentLink_UsesCurrentMarkdownFolderForRelativePaths()
  {
    var resolver = new MarkdownPathResolver();

    var resolved = resolver.ResolveDocumentLink("/Users/example/docs/guide.md", "../README.md");

    Assert.Equal("/Users/example/README.md", resolved);
  }

  [Fact]
  public void ResolveImagePath_UsesCurrentMarkdownFolderForRelativeImages()
  {
    var resolver = new MarkdownPathResolver();

    var resolved = resolver.ResolveImagePath("/Users/example/docs/guide.md", "images/diagram.png");

    Assert.Equal("/Users/example/docs/images/diagram.png", resolved);
  }

  [Fact]
  public void ResolveDocumentLink_LeavesExternalUrlAndAnchorUnchanged()
  {
    var resolver = new MarkdownPathResolver();

    Assert.Equal("https://example.com", resolver.ResolveDocumentLink("/Users/example/guide.md", "https://example.com"));
    Assert.Equal("#architecture", resolver.ResolveDocumentLink("/Users/example/guide.md", "#architecture"));
  }
}

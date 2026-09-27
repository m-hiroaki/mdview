using mdview.Application.UseCases;

namespace mdview.Application.Tests;

public class MarkdownPathResolverTests
{
  [Fact]
  public void ResolveDocumentLink_UsesCurrentMarkdownFolderForRelativePaths()
  {
    var resolver = new MarkdownPathResolver();
    var currentMarkdownPath = Path.Combine(Path.GetTempPath(), "mdview", "docs", "guide.md");

    var resolved = resolver.ResolveDocumentLink(currentMarkdownPath, "../README.md");

    Assert.Equal(Path.Combine(Path.GetTempPath(), "mdview", "README.md"), resolved);
  }

  [Fact]
  public void ResolveImagePath_UsesCurrentMarkdownFolderForRelativeImages()
  {
    var resolver = new MarkdownPathResolver();
    var currentMarkdownPath = Path.Combine(Path.GetTempPath(), "mdview", "docs", "guide.md");

    var resolved = resolver.ResolveImagePath(currentMarkdownPath, "images/diagram.png");

    Assert.Equal(Path.Combine(Path.GetTempPath(), "mdview", "docs", "images", "diagram.png"), resolved);
  }

  [Fact]
  public void ResolveDocumentLink_PreservesFragmentWhenResolvingRelativeMarkdownFiles()
  {
    var resolver = new MarkdownPathResolver();
    var currentMarkdownPath = Path.Combine(Path.GetTempPath(), "mdview", "docs", "guide.md");

    var resolved = resolver.ResolveDocumentLink(currentMarkdownPath, "../README.md#start");

    Assert.Equal(Path.Combine(Path.GetTempPath(), "mdview", "README.md") + "#start", resolved);
  }

  [Fact]
  public void ResolveDocumentLink_LeavesExternalUrlAndAnchorUnchanged()
  {
    var resolver = new MarkdownPathResolver();
    var currentMarkdownPath = Path.Combine(Path.GetTempPath(), "mdview", "guide.md");

    Assert.Equal("https://example.com", resolver.ResolveDocumentLink(currentMarkdownPath, "https://example.com"));
    Assert.Equal("#architecture", resolver.ResolveDocumentLink(currentMarkdownPath, "#architecture"));
  }
}

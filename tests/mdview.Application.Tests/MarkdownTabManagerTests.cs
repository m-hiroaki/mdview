using mdview.Application.Models;

namespace mdview.Application.Tests;

public class MarkdownTabManagerTests
{
  [Fact]
  public void OpenOrActivate_WhenPathAlreadyOpen_ActivatesExistingTabWithoutDuplicate()
  {
    var manager = new MarkdownTabManager();
    var first = manager.OpenOrActivate("/Users/example/README.md", "README.md", new MarkdownDocumentModel([]));
    var second = manager.OpenOrActivate("/Users/example/README.md", "README.md", new MarkdownDocumentModel([]));

    Assert.Single(manager.Tabs);
    Assert.Same(first, manager.ActiveTab);
    Assert.Same(first, second);
  }

  [Fact]
  public void CloseActiveTab_WhenRemoved_ActivatesAdjacentTab()
  {
    var manager = new MarkdownTabManager();
    var first = manager.OpenOrActivate("/Users/example/one.md", "one.md", new MarkdownDocumentModel([]));
    var second = manager.OpenOrActivate("/Users/example/two.md", "two.md", new MarkdownDocumentModel([]));

    manager.CloseTab(second);

    Assert.Single(manager.Tabs);
    Assert.Same(first, manager.ActiveTab);
  }

  [Fact]
  public void NormalizePath_UsesCanonicalComparisonAcrossDifferentCasing()
  {
    var manager = new MarkdownTabManager();
    var first = manager.OpenOrActivate("/Users/example/Guide.md", "Guide.md", new MarkdownDocumentModel([]));
    var second = manager.OpenOrActivate("/users/example/guide.md", "Guide.md", new MarkdownDocumentModel([]));

    Assert.Single(manager.Tabs);
    Assert.Same(first, second);
  }
}

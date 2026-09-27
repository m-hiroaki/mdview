using mdview.Domain.Models;

namespace mdview.Domain.Tests;

public class MarkdownDocumentTests
{
  [Fact]
  public void Constructor_PreservesFilePathAndContent()
  {
    var document = new MarkdownDocument("README.md", "# Readme");

    Assert.Equal("README.md", document.FilePath);
    Assert.Equal("# Readme", document.Content);
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  public void Constructor_RejectsMissingFilePath(string filePath)
  {
    Assert.Throws<ArgumentException>(() => new MarkdownDocument(filePath, "content"));
  }

  [Fact]
  public void Constructor_RejectsNullContent()
  {
    Assert.Throws<ArgumentNullException>(() => new MarkdownDocument("README.md", null!));
  }
}

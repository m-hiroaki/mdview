using mdview.Infrastructure.FileSystem;

namespace mdview.Infrastructure.Tests;

public class FileAssociationServiceTests
{
  [Fact]
  public async Task NonWindows_DoesNotChangeFileAssociations()
  {
    var service = new FileAssociationService();

    if (!OperatingSystem.IsWindows())
    {
      Assert.False(service.IsSupported);
      Assert.False(await service.RegisterMarkdownAssociationAsync("/tmp/mdview"));
    }
  }
}

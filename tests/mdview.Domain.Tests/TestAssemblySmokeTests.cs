using System.Reflection;

namespace mdview.Domain.Tests;

public class TestAssemblySmokeTests
{
  [Fact]
  public void TestAssemblyHasExpectedName()
  {
    Assert.Equal("mdview.Domain.Tests", Assembly.GetExecutingAssembly().GetName().Name);
  }
}

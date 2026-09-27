using System.Reflection;

namespace mdview.Infrastructure.Tests;

public class TestAssemblySmokeTests
{
    [Fact]
    public void TestAssemblyHasExpectedName()
    {
        Assert.Equal("mdview.Infrastructure.Tests", Assembly.GetExecutingAssembly().GetName().Name);
    }
}

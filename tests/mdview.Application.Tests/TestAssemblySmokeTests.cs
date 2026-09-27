using System.Reflection;

namespace mdview.Application.Tests;

public class TestAssemblySmokeTests
{
    [Fact]
    public void TestAssemblyHasExpectedName()
    {
        Assert.Equal("mdview.Application.Tests", Assembly.GetExecutingAssembly().GetName().Name);
    }
}

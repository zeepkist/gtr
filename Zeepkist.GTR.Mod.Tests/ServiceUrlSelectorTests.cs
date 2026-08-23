using TNRD.Zeepkist.GTR.Configuration;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class ServiceUrlSelectorTests
{
    [Theory]
    [InlineData(false, false, "production")]
    [InlineData(false, true, "alternative")]
    [InlineData(true, false, "local")]
    [InlineData(true, true, "local")]
    public void SelectUsesExpectedPrecedence(bool useLocal, bool useAlternative, string expected)
    {
        string result = ServiceUrlSelector.Select(
            useLocal,
            useAlternative,
            "production",
            "alternative",
            "local");

        Assert.Equal(expected, result);
    }
}

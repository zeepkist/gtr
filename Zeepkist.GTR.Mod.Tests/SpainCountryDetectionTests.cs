using TNRD.Zeepkist.GTR.Dialogs;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class SpainCountryDetectionTests
{
    [Theory]
    [InlineData("{\"country\":\"ES\"}")]
    [InlineData("{\"country\":\"es\"}")]
    [InlineData("{\"ip\":\"203.0.113.1\",\"country\":\"ES\"}")]
    public void SpanishCountryCodeIsDetected(string response)
    {
        Assert.True(SpainCountryDetection.IsSpanishResponse(response));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"country\":\"GB\"}")]
    [InlineData("{\"country\":\"ESP\"}")]
    public void OtherResponsesAreNotDetectedAsSpain(string response)
    {
        Assert.False(SpainCountryDetection.IsSpanishResponse(response));
    }
}

using TNRD.Zeepkist.GTR.Ghosting.Recording;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class RecordSubmissionEligibilityTests
{
    [Theory]
    [InlineData("trtm-config-")]
    [InlineData("trtm-config-random-level")]
    public void RejectsNonReplayableTrtmLevels(string levelUid)
    {
        Assert.False(RecordSubmissionEligibility.ShouldSubmit(levelUid));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("workshop-level")]
    [InlineData("TRTM-CONFIG-random-level")]
    public void AllowsOtherLevels(string levelUid)
    {
        Assert.True(RecordSubmissionEligibility.ShouldSubmit(levelUid));
    }
}

using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class GhostFrameSearchTests
{
    [Fact]
    public void TryGetFrameSample_ReturnsFalseForNoFrames()
    {
        Assert.False(GhostFrameSearch.TryGetFrameSample(0, 1f, _ => 0f, out _));
    }

    [Fact]
    public void TryGetFrameSample_ClampsSingleFrame()
    {
        float[] frameTimes = [2f];

        Assert.True(GhostFrameSearch.TryGetFrameSample(
            frameTimes.Length,
            10f,
            i => frameTimes[i],
            out GhostFrameSample sample));
        Assert.Equal(0, sample.CurrentIndex);
        Assert.Equal(0, sample.NextIndex);
        Assert.Equal(0f, sample.Interpolation);
    }

    [Theory]
    [InlineData(-1f, 0, 0, 0f)]
    [InlineData(0f, 0, 1, 0f)]
    [InlineData(0.5f, 0, 1, 0.5f)]
    [InlineData(1f, 1, 2, 0f)]
    [InlineData(2f, 2, 2, 0f)]
    [InlineData(3f, 2, 2, 0f)]
    public void TryGetFrameSample_SelectsFloorAndInterpolationBracket(
        float time,
        int expectedCurrent,
        int expectedNext,
        float expectedInterpolation)
    {
        float[] frameTimes = [0f, 1f, 2f];

        Assert.True(GhostFrameSearch.TryGetFrameSample(
            frameTimes.Length,
            time,
            i => frameTimes[i],
            out GhostFrameSample sample));
        Assert.Equal(expectedCurrent, sample.CurrentIndex);
        Assert.Equal(expectedNext, sample.NextIndex);
        Assert.Equal(expectedInterpolation, sample.Interpolation, 5);
    }

    [Fact]
    public void TryGetFrameSample_UsesLastDuplicateAtExactTimestamp()
    {
        float[] frameTimes = [0f, 1f, 1f, 1f, 2f];

        Assert.True(GhostFrameSearch.TryGetFrameSample(
            frameTimes.Length,
            1f,
            i => frameTimes[i],
            out GhostFrameSample sample));
        Assert.Equal(3, sample.CurrentIndex);
        Assert.Equal(4, sample.NextIndex);
        Assert.Equal(0f, sample.Interpolation);
    }

    [Fact]
    public void TryGetFrameSample_SamplesNinetyHertzSourceAtSixtyHertzRenderTimes()
    {
        float[] frameTimes = Enumerable.Range(0, 10).Select(i => i * 0.011f).ToArray();
        float renderTime = 2f / 60f;

        Assert.True(GhostFrameSearch.TryGetFrameSample(
            frameTimes.Length,
            renderTime,
            i => frameTimes[i],
            out GhostFrameSample sample));
        Assert.Equal(3, sample.CurrentIndex);
        Assert.Equal(4, sample.NextIndex);
        Assert.InRange(sample.Interpolation, 0.0302f, 0.0304f);
    }

    [Fact]
    public void TryGetFrameSample_ForwardHintWalksAcrossSkippedAndDuplicateFrames()
    {
        float[] frameTimes = [0f, 0.011f, 0.022f, 0.022f, 0.033f, 0.044f];

        Assert.True(GhostFrameSearch.TryGetFrameSample(
            frameTimes.Length,
            2f / 60f,
            1,
            i => frameTimes[i],
            out GhostFrameSample sample));
        Assert.Equal(4, sample.CurrentIndex);
        Assert.Equal(5, sample.NextIndex);
        Assert.InRange(sample.Interpolation, 0.0302f, 0.0304f);
    }

    [Fact]
    public void FindFirstFrameIndexAtOrAfterTime_ReturnsFirstMatchingFrame()
    {
        float[] frameTimes = { 0f, 1f, 2f, 4f, 8f };

        Assert.Equal(1, GhostFrameSearch.FindFirstFrameIndexAtOrAfterTime(frameTimes.Length, 0.5f, i => frameTimes[i]));
        Assert.Equal(2, GhostFrameSearch.FindFirstFrameIndexAtOrAfterTime(frameTimes.Length, 2f, i => frameTimes[i]));
        Assert.Equal(3, GhostFrameSearch.FindFirstFrameIndexAtOrAfterTime(frameTimes.Length, 3f, i => frameTimes[i]));
        Assert.Equal(4, GhostFrameSearch.FindFirstFrameIndexAtOrAfterTime(frameTimes.Length, 8f, i => frameTimes[i]));
    }

    [Fact]
    public void FindFirstFrameIndexAtOrAfterTime_WorksWithFrameList()
    {
        float[] frameTimes = { 0f, 1f, 2f, 4f, 8f };

        Assert.Equal(3, GhostFrameSearch.FindFirstFrameIndexAtOrAfterTime(frameTimes, 3.5f, time => time));
    }

    [Fact]
    public void TryGetAdjacentFrameTime_StepsForwardFromMidSegment()
    {
        float[] frameTimes = { 0f, 1f, 2f, 4f, 8f };
        const float epsilon = 0.005f;

        Assert.True(GhostFrameSearch.TryGetAdjacentFrameTime(
            frameTimes.Length,
            1.5f,
            1,
            epsilon,
            i => frameTimes[i],
            out float forwardTime));
        Assert.Equal(2f, forwardTime);
    }

    [Fact]
    public void TryGetAdjacentFrameTime_StepsBackwardFromMidSegment()
    {
        float[] frameTimes = { 0f, 1f, 2f, 4f, 8f };
        const float epsilon = 0.005f;

        Assert.True(GhostFrameSearch.TryGetAdjacentFrameTime(
            frameTimes.Length,
            1.5f,
            -1,
            epsilon,
            i => frameTimes[i],
            out float backwardTime));
        Assert.Equal(1f, backwardTime);
    }

    [Fact]
    public void TryGetAdjacentFrameTime_StepsForwardFromExactFrameBoundary()
    {
        float[] frameTimes = { 0f, 1f, 2f, 4f, 8f };
        const float epsilon = 0.005f;

        Assert.True(GhostFrameSearch.TryGetAdjacentFrameTime(
            frameTimes.Length,
            2f,
            1,
            epsilon,
            i => frameTimes[i],
            out float forwardTime));
        Assert.Equal(4f, forwardTime);
    }

    [Fact]
    public void TryGetAdjacentFrameTime_StepsBackwardFromExactFrameBoundary()
    {
        float[] frameTimes = { 0f, 1f, 2f, 4f, 8f };
        const float epsilon = 0.005f;

        Assert.True(GhostFrameSearch.TryGetAdjacentFrameTime(
            frameTimes.Length,
            2f,
            -1,
            epsilon,
            i => frameTimes[i],
            out float backwardTime));
        Assert.Equal(1f, backwardTime);
    }

    [Fact]
    public void TryGetAdjacentFrameTime_ReturnsFalseAtStartAndEnd()
    {
        float[] frameTimes = { 0f, 1f, 2f, 4f, 8f };
        const float epsilon = 0.005f;

        Assert.False(GhostFrameSearch.TryGetAdjacentFrameTime(
            frameTimes.Length,
            0f,
            -1,
            epsilon,
            i => frameTimes[i],
            out _));

        Assert.False(GhostFrameSearch.TryGetAdjacentFrameTime(
            frameTimes.Length,
            8f,
            1,
            epsilon,
            i => frameTimes[i],
            out _));
    }

    [Fact]
    public void TryGetAdjacentFrameTime_ReturnsFalseForSingleFrame()
    {
        float[] frameTimes = [1f];

        Assert.False(GhostFrameSearch.TryGetAdjacentFrameTime(
            frameTimes.Length,
            1f,
            1,
            0.005f,
            i => frameTimes[i],
            out _));
        Assert.False(GhostFrameSearch.TryGetAdjacentFrameTime(
            frameTimes.Length,
            1f,
            -1,
            0.005f,
            i => frameTimes[i],
            out _));
    }
}

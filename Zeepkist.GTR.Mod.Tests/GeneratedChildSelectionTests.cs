using TNRD.Zeepkist.GTR.Ghosting.Playback;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public sealed class GeneratedChildSelectionTests
{
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 1, true)]
    [InlineData(0, 2, false)]
    [InlineData(1, 2, true)]
    public void CurrentAuxiliaryRootIsLastChild(
        int siblingIndex,
        int childCount,
        bool expected)
    {
        Assert.Equal(
            expected,
            GeneratedChildSelection.IsCurrentAuxiliaryRoot(siblingIndex, childCount));
    }

    [Theory]
    [InlineData(0, 2, false, false, false)]
    [InlineData(1, 2, false, false, false)]
    [InlineData(1, 2, true, false, true)]
    [InlineData(1, 2, false, true, true)]
    [InlineData(0, 2, true, true, true)]
    [InlineData(1, 2, true, true, true)]
    [InlineData(0, 3, true, true, false)]
    [InlineData(1, 3, true, true, true)]
    [InlineData(2, 3, true, true, true)]
    public void CurrentCosmeticRootsAreTrailingRequestedChildren(
        int siblingIndex,
        int childCount,
        bool hasHat,
        bool hasGlasses,
        bool expected)
    {
        Assert.Equal(
            expected,
            GeneratedChildSelection.IsCurrentCosmeticRoot(
                siblingIndex,
                childCount,
                hasHat,
                hasGlasses));
    }
}

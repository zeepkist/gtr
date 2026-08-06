using TNRD.Zeepkist.GTR.LevelBrowser;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class LevelBrowserPageWindowTests
{
    [Fact]
    public void SinglePageReturnsOnlyFirstPage()
    {
        Assert.Equal(new[] { 0 }, LevelBrowserPageWindow.Build(0, 1));
        Assert.Equal(new[] { 0 }, LevelBrowserPageWindow.Build(0, 0));
    }

    [Fact]
    public void FewPagesHaveNoEllipsis()
    {
        Assert.Equal(new[] { 0, 1, 2 }, LevelBrowserPageWindow.Build(1, 3));
        // current=1 pulls in neighbors 0..2 plus last → contiguous 0..3
        Assert.Equal(new[] { 0, 1, 2, 3 }, LevelBrowserPageWindow.Build(1, 4));
    }

    [Fact]
    public void CurrentNearStartShowsTrailingEllipsis()
    {
        // pages: 0,1,2 … 19  (current=0 → neighbors 0,1; first+last)
        Assert.Equal(
            new[] { 0, 1, LevelBrowserPageWindow.Ellipsis, 19 },
            LevelBrowserPageWindow.Build(0, 20));
    }

    [Fact]
    public void CurrentInMiddleShowsBothEllipses()
    {
        // pages: 0 … 3,4,5 … 19  (current=4)
        Assert.Equal(
            new[] { 0, LevelBrowserPageWindow.Ellipsis, 3, 4, 5, LevelBrowserPageWindow.Ellipsis, 19 },
            LevelBrowserPageWindow.Build(4, 20));
    }

    [Fact]
    public void CurrentNearEndShowsLeadingEllipsis()
    {
        // pages: 0 … 17,18,19  (current=19 → neighbors 18,19)
        Assert.Equal(
            new[] { 0, LevelBrowserPageWindow.Ellipsis, 18, 19 },
            LevelBrowserPageWindow.Build(19, 20));
    }

    [Fact]
    public void ClampsOutOfRangeCurrentPage()
    {
        Assert.Equal(
            new[] { 0, 1, LevelBrowserPageWindow.Ellipsis, 19 },
            LevelBrowserPageWindow.Build(-5, 20));
        Assert.Equal(
            new[] { 0, LevelBrowserPageWindow.Ellipsis, 18, 19 },
            LevelBrowserPageWindow.Build(99, 20));
    }
}

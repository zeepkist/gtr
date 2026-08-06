using TNRD.Zeepkist.GTR.LevelBrowser;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class LevelBrowseRowTests
{
    [Theory]
    [InlineData("0", 0UL)]
    [InlineData("123456789", 123456789UL)]
    [InlineData("18446744073709551615", 18446744073709551615UL)]
    [InlineData("  42  ", 42UL)]
    public void WorkshopIdParsesBigIntString(string raw, ulong expected)
    {
        Assert.Equal(expected, WorkshopIdParser.Parse(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-number")]
    [InlineData("-5")]
    public void WorkshopIdFallsBackToZeroForInvalidInput(string raw)
    {
        Assert.Equal(0UL, WorkshopIdParser.Parse(raw));
    }

    [Fact]
    public void RowProjectsSelectionFields()
    {
        var row = new LevelBrowseRow("uid", 7UL, "Name", "Author", "http://img");

        LevelBrowserSelection selection = row.ToSelection();

        Assert.Equal("uid", selection.FileUid);
        Assert.Equal(7UL, selection.WorkshopId);
        Assert.Equal("Name", selection.Name);
        Assert.Equal("Author", selection.FileAuthor);
    }

    [Theory]
    [InlineData("http://img", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void HasThumbnailReflectsImageUrl(string imageUrl, bool expected)
    {
        var row = new LevelBrowseRow("uid", 1UL, "Name", "Author", imageUrl);

        Assert.Equal(expected, row.HasThumbnail);
    }

    [Fact]
    public void PageReportsMoreWhenTotalExceedsFetched()
    {
        var rows = new[] { new LevelBrowseRow("a", 1UL, "A", "x", null) };
        var page = new LevelBrowsePage(rows, totalCount: 30);

        Assert.False(page.IsEmpty);
        Assert.True(page.HasMoreAfter(0));
        Assert.False(page.HasMoreAfter(29));
    }

    [Fact]
    public void EmptyPageIsEmptyAndHasNoMore()
    {
        var page = new LevelBrowsePage(new LevelBrowseRow[0], totalCount: 0);

        Assert.True(page.IsEmpty);
        Assert.False(page.HasMoreAfter(0));
    }
}

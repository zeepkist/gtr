using TNRD.Zeepkist.GTR.LevelBrowser;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class LevelItemsBrowseQueryBuilderTests
{
    [Fact]
    public void HygieneConstantsAreAlwaysDeletedFalseAndPublicTrue()
    {
        Assert.False(LevelItemsBrowseQuery.HygieneDeletedEqualTo);
        Assert.True(LevelItemsBrowseQuery.HygienePubliclyVisibleEqualTo);
    }

    [Fact]
    public void DefaultOrderIsDateCreatedDesc()
    {
        Assert.Equal("DATE_CREATED_DESC", LevelItemsBrowseQuery.OrderBy);
    }

    [Fact]
    public void NameAndAuthorFiltersApplyWhenNonEmpty()
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build("Rock", "Matt");

        Assert.Equal("Rock", query.NameIncludesInsensitive);
        Assert.Equal("Matt", query.FileAuthorIncludesInsensitive);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankFiltersBecomeNullPredicates(string blank)
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(blank, blank);

        Assert.Null(query.NameIncludesInsensitive);
        Assert.Null(query.FileAuthorIncludesInsensitive);
    }

    [Fact]
    public void FilterTextIsTrimmed()
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build("  spicy  ", "  matt  ");

        Assert.Equal("spicy", query.NameIncludesInsensitive);
        Assert.Equal("matt", query.FileAuthorIncludesInsensitive);
    }

    [Fact]
    public void PaginationUsesFirstAndOffsetFromPageAndPageSize()
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(null, null, page: 3, pageSize: 10);

        Assert.Equal(10, query.First);
        Assert.Equal(30, query.Offset);
    }

    [Fact]
    public void FirstPageHasZeroOffset()
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(null, null, page: 0, pageSize: 12);

        Assert.Equal(12, query.First);
        Assert.Equal(0, query.Offset);
    }

    [Fact]
    public void DefaultPageSizeIsAppliedWhenUnspecified()
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(null, null);

        Assert.Equal(LevelItemsBrowseQueryBuilder.DefaultPageSize, query.First);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void NegativePageClampsToZeroOffset(int page)
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(null, null, page, pageSize: 12);

        Assert.Equal(0, query.Offset);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NonPositivePageSizeFallsBackToDefault(int pageSize)
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(null, null, page: 1, pageSize: pageSize);

        Assert.Equal(LevelItemsBrowseQueryBuilder.DefaultPageSize, query.First);
        Assert.Equal(LevelItemsBrowseQueryBuilder.DefaultPageSize, query.Offset);
    }
}

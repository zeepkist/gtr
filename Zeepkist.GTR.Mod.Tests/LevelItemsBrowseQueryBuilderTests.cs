using System;
using TNRD.Zeepkist.GTR.LevelBrowser;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class LevelItemsBrowseQueryBuilderTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void HygieneConstantsAreAlwaysDeletedFalseAndPublicTrue()
    {
        Assert.False(LevelItemsBrowseQuery.HygieneDeletedEqualTo);
        Assert.True(LevelItemsBrowseQuery.HygienePubliclyVisibleEqualTo);
    }

    [Fact]
    public void TopRatedNetScoreIsTen()
    {
        Assert.Equal(10, LevelItemsBrowseQuery.TopRatedNetScore);
    }

    [Fact]
    public void NameAndAuthorFiltersApplyWhenNonEmpty()
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build("Rock", "Matt", nowUtc: FixedNow);

        Assert.Equal("Rock", query.NameIncludesInsensitive);
        Assert.Equal("Matt", query.FileAuthorIncludesInsensitive);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankFiltersBecomeNullPredicates(string blank)
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(blank, blank, nowUtc: FixedNow);

        Assert.Null(query.NameIncludesInsensitive);
        Assert.Null(query.FileAuthorIncludesInsensitive);
    }

    [Fact]
    public void FilterTextIsTrimmed()
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build("  spicy  ", "  matt  ", nowUtc: FixedNow);

        Assert.Equal("spicy", query.NameIncludesInsensitive);
        Assert.Equal("matt", query.FileAuthorIncludesInsensitive);
    }

    [Fact]
    public void PaginationUsesFirstAndOffsetFromPageAndPageSize()
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(null, null, page: 3, pageSize: 10, nowUtc: FixedNow);

        Assert.Equal(10, query.First);
        Assert.Equal(30, query.Offset);
    }

    [Fact]
    public void FirstPageHasZeroOffset()
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(null, null, page: 0, pageSize: 12, nowUtc: FixedNow);

        Assert.Equal(12, query.First);
        Assert.Equal(0, query.Offset);
    }

    [Fact]
    public void DefaultPageSizeIsAppliedWhenUnspecified()
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(null, null, nowUtc: FixedNow);

        Assert.Equal(LevelItemsBrowseQueryBuilder.DefaultPageSize, query.First);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void NegativePageClampsToZeroOffset(int page)
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(null, null, page, pageSize: 12, nowUtc: FixedNow);

        Assert.Equal(0, query.Offset);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NonPositivePageSizeFallsBackToDefault(int pageSize)
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(null, null, page: 1, pageSize: pageSize, nowUtc: FixedNow);

        Assert.Equal(LevelItemsBrowseQueryBuilder.DefaultPageSize, query.First);
        Assert.Equal(LevelItemsBrowseQueryBuilder.DefaultPageSize, query.Offset);
    }

    [Fact]
    public void DefaultsAreNewestAnyTimeAnyLengthAnyRating()
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(null, null, nowUtc: FixedNow);

        Assert.Equal(LevelBrowseSort.Newest, query.Sort);
        Assert.Null(query.DateCreatedAfter);
        Assert.Null(query.TimeMin);
        Assert.Null(query.TimeMax);
        Assert.Equal(LevelBrowseRating.Any, query.Rating);
    }

    [Theory]
    [InlineData(LevelBrowseDateRange.PastWeek, -7)]
    [InlineData(LevelBrowseDateRange.PastMonth, -30)]
    [InlineData(LevelBrowseDateRange.PastYear, -365)]
    public void DatePresetResolvesAgainstNowUtc(LevelBrowseDateRange range, int daysOffset)
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(
            null, null, dateRange: range, nowUtc: FixedNow);

        Assert.Equal(FixedNow.AddDays(daysOffset), query.DateCreatedAfter);
    }

    [Fact]
    public void AnyTimeLeavesDateCreatedAfterNull()
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(
            null, null, dateRange: LevelBrowseDateRange.AnyTime, nowUtc: FixedNow);

        Assert.Null(query.DateCreatedAfter);
    }

    [Fact]
    public void ShortTrackMapsToMaxThirtySeconds()
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(
            null, null, trackLength: LevelBrowseTrackLength.Short, nowUtc: FixedNow);

        Assert.Null(query.TimeMin);
        Assert.Equal(30d, query.TimeMax);
    }

    [Fact]
    public void MediumTrackMapsToThirtyThroughNinety()
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(
            null, null, trackLength: LevelBrowseTrackLength.Medium, nowUtc: FixedNow);

        Assert.Equal(30d, query.TimeMin);
        Assert.Equal(90d, query.TimeMax);
    }

    [Fact]
    public void LongTrackMapsToMinNinetySeconds()
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(
            null, null, trackLength: LevelBrowseTrackLength.Long, nowUtc: FixedNow);

        Assert.Equal(90d, query.TimeMin);
        Assert.Null(query.TimeMax);
    }

    [Fact]
    public void SortPassthrough()
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(
            null, null, sort: LevelBrowseSort.NameAsc, nowUtc: FixedNow);

        Assert.Equal(LevelBrowseSort.NameAsc, query.Sort);
    }

    [Fact]
    public void RatingPassthrough()
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(
            null, null, rating: LevelBrowseRating.WellRated, nowUtc: FixedNow);

        Assert.Equal(LevelBrowseRating.WellRated, query.Rating);
    }

    [Fact]
    public void OwnershipFiltersDefaultOff()
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(
            null, null, ownerSteamId: "76561198000000000", nowUtc: FixedNow);

        Assert.Null(query.AuthorIdEqualTo);
        Assert.Null(query.ExcludePersonalBestSteamId);
        Assert.False(query.RequireNoRecords);
    }

    [Fact]
    public void OwnLevelsOnlySetsAuthorIdEqualToSteamId()
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(
            null,
            null,
            ownLevelsOnly: true,
            ownerSteamId: "76561198000000000",
            nowUtc: FixedNow);

        Assert.Equal("76561198000000000", query.AuthorIdEqualTo);
        Assert.Null(query.ExcludePersonalBestSteamId);
        Assert.False(query.RequireNoRecords);
    }

    [Fact]
    public void WithoutMyPersonalBestSetsExcludeSteamId()
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(
            null,
            null,
            withoutMyPersonalBest: true,
            ownerSteamId: "76561198000000000",
            nowUtc: FixedNow);

        Assert.Null(query.AuthorIdEqualTo);
        Assert.Equal("76561198000000000", query.ExcludePersonalBestSteamId);
        Assert.False(query.RequireNoRecords);
    }

    [Fact]
    public void WithoutRecordsSetsRequireNoRecords()
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(
            null, null, withoutRecords: true, nowUtc: FixedNow);

        Assert.True(query.RequireNoRecords);
        Assert.Null(query.AuthorIdEqualTo);
        Assert.Null(query.ExcludePersonalBestSteamId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OwnLevelsOnlyWithBlankSteamIdResolvesToNull(string blankSteamId)
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(
            null,
            null,
            ownLevelsOnly: true,
            withoutMyPersonalBest: true,
            ownerSteamId: blankSteamId,
            nowUtc: FixedNow);

        Assert.Null(query.AuthorIdEqualTo);
        Assert.Null(query.ExcludePersonalBestSteamId);
    }

    [Fact]
    public void OwnershipFiltersTrimSteamId()
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(
            null,
            null,
            ownLevelsOnly: true,
            withoutMyPersonalBest: true,
            ownerSteamId: "  76561198000000000  ",
            nowUtc: FixedNow);

        Assert.Equal("76561198000000000", query.AuthorIdEqualTo);
        Assert.Equal("76561198000000000", query.ExcludePersonalBestSteamId);
    }

    [Fact]
    public void AllOwnershipFiltersCombine()
    {
        LevelItemsBrowseQuery query = LevelItemsBrowseQueryBuilder.Build(
            null,
            null,
            ownLevelsOnly: true,
            withoutMyPersonalBest: true,
            withoutRecords: true,
            ownerSteamId: "76561198000000000",
            nowUtc: FixedNow);

        Assert.Equal("76561198000000000", query.AuthorIdEqualTo);
        Assert.Equal("76561198000000000", query.ExcludePersonalBestSteamId);
        Assert.True(query.RequireNoRecords);
    }
}

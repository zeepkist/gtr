using System;

namespace TNRD.Zeepkist.GTR.LevelBrowser;

/// <summary>
/// Builds the default <c>levelItems</c> browse arguments from raw discovery inputs: always-on
/// hygiene (applied by the query object), case-insensitive substring matchers for name and author
/// when non-empty, resolved date/track-length/rating predicates, sort, and
/// <c>first</c>/<c>offset</c> pagination.
/// </summary>
public static class LevelItemsBrowseQueryBuilder
{
    /// <summary>Default number of rows per page.</summary>
    public const int DefaultPageSize = 12;

    /// <summary>Short track upper bound (seconds) — exclusive of Medium.</summary>
    public const double ShortTrackMaxSeconds = 30d;

    /// <summary>Medium track upper bound (seconds) — exclusive of Long.</summary>
    public const double MediumTrackMaxSeconds = 90d;

    /// <summary>
    /// Produces the resolved <see cref="LevelItemsBrowseQuery"/>. Blank/whitespace filter text
    /// becomes <c>null</c> (no predicate); page and pageSize are clamped to sane minimums;
    /// date presets are resolved against <paramref name="nowUtc"/>. Ownership/PB filters only
    /// resolve when <paramref name="ownerSteamId"/> is non-blank.
    /// </summary>
    public static LevelItemsBrowseQuery Build(
        string name,
        string fileAuthor,
        int page = 0,
        int pageSize = DefaultPageSize,
        LevelBrowseSort sort = LevelBrowseSort.Newest,
        LevelBrowseDateRange dateRange = LevelBrowseDateRange.AnyTime,
        LevelBrowseTrackLength trackLength = LevelBrowseTrackLength.Any,
        LevelBrowseRating rating = LevelBrowseRating.Any,
        bool ownLevelsOnly = false,
        bool withoutMyPersonalBest = false,
        bool withoutRecords = false,
        string ownerSteamId = null,
        DateTimeOffset? nowUtc = null)
    {
        if (page < 0)
            page = 0;
        if (pageSize < 1)
            pageSize = DefaultPageSize;

        ResolveTrackLength(trackLength, out double? timeMin, out double? timeMax);

        string steamId = Normalize(ownerSteamId);

        return new LevelItemsBrowseQuery(
            Normalize(name),
            Normalize(fileAuthor),
            ResolveDateCreatedAfter(dateRange, nowUtc ?? DateTimeOffset.UtcNow),
            timeMin,
            timeMax,
            rating,
            ownLevelsOnly ? steamId : null,
            withoutMyPersonalBest ? steamId : null,
            withoutRecords,
            sort,
            pageSize,
            page * pageSize);
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }

    private static DateTimeOffset? ResolveDateCreatedAfter(LevelBrowseDateRange dateRange, DateTimeOffset nowUtc)
    {
        switch (dateRange)
        {
            case LevelBrowseDateRange.PastWeek:
                return nowUtc.AddDays(-7);
            case LevelBrowseDateRange.PastMonth:
                return nowUtc.AddDays(-30);
            case LevelBrowseDateRange.PastYear:
                return nowUtc.AddDays(-365);
            default:
                return null;
        }
    }

    private static void ResolveTrackLength(LevelBrowseTrackLength trackLength, out double? timeMin, out double? timeMax)
    {
        switch (trackLength)
        {
            case LevelBrowseTrackLength.Short:
                timeMin = null;
                timeMax = ShortTrackMaxSeconds;
                return;
            case LevelBrowseTrackLength.Medium:
                timeMin = ShortTrackMaxSeconds;
                timeMax = MediumTrackMaxSeconds;
                return;
            case LevelBrowseTrackLength.Long:
                timeMin = MediumTrackMaxSeconds;
                timeMax = null;
                return;
            default:
                timeMin = null;
                timeMax = null;
                return;
        }
    }
}

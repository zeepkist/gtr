using System;

namespace TNRD.Zeepkist.GTR.LevelBrowser;

/// <summary>
/// The resolved argument set for a default <c>levelItems</c> browse request. Produced by
/// <see cref="LevelItemsBrowseQueryBuilder"/> so the choice of hygiene, matchers, order, and
/// pagination is a pure, testable value the GraphQL service simply forwards.
/// </summary>
public sealed class LevelItemsBrowseQuery
{
    /// <summary>Always-on hygiene: only non-soft-deleted rows (<c>deleted: { equalTo: false }</c>).</summary>
    public const bool HygieneDeletedEqualTo = false;

    /// <summary>Always-on hygiene: only public levels (<c>level: { publiclyVisible: { equalTo: true } }</c>).</summary>
    public const bool HygienePubliclyVisibleEqualTo = true;

    /// <summary>
    /// Net vote-sum threshold used when <see cref="Rating"/> is <see cref="LevelBrowseRating.TopRated"/>.
    /// </summary>
    public const int TopRatedNetScore = 10;

    /// <summary>
    /// Case-insensitive substring to match against <c>name</c>, or <c>null</c> when no name filter
    /// is active. Applied as <c>name: { includesInsensitive: ... }</c>.
    /// </summary>
    public string NameIncludesInsensitive { get; }

    /// <summary>
    /// Case-insensitive substring to match against <c>fileAuthor</c>, or <c>null</c> when no author
    /// filter is active. Applied as <c>fileAuthor: { includesInsensitive: ... }</c>.
    /// </summary>
    public string FileAuthorIncludesInsensitive { get; }

    /// <summary>Resolved date-created lower bound, or <c>null</c> when Any time.</summary>
    public DateTimeOffset? DateCreatedAfter { get; }

    /// <summary>Inclusive lower bound on <c>validationTimeAuthor</c> (seconds), or <c>null</c>.</summary>
    public double? TimeMin { get; }

    /// <summary>Inclusive upper bound on <c>validationTimeAuthor</c> (seconds), or <c>null</c>.</summary>
    public double? TimeMax { get; }

    /// <summary>Vote-quality rating preset.</summary>
    public LevelBrowseRating Rating { get; }

    /// <summary>Sort order for the page.</summary>
    public LevelBrowseSort Sort { get; }

    /// <summary>Page size (<c>first</c>), following the existing <c>first</c>/<c>offset</c> paging style.</summary>
    public int First { get; }

    /// <summary>Row offset (<c>offset</c>) = page * pageSize.</summary>
    public int Offset { get; }

    internal LevelItemsBrowseQuery(
        string nameIncludesInsensitive,
        string fileAuthorIncludesInsensitive,
        DateTimeOffset? dateCreatedAfter,
        double? timeMin,
        double? timeMax,
        LevelBrowseRating rating,
        LevelBrowseSort sort,
        int first,
        int offset)
    {
        NameIncludesInsensitive = nameIncludesInsensitive;
        FileAuthorIncludesInsensitive = fileAuthorIncludesInsensitive;
        DateCreatedAfter = dateCreatedAfter;
        TimeMin = timeMin;
        TimeMax = timeMax;
        Rating = rating;
        Sort = sort;
        First = first;
        Offset = offset;
    }
}

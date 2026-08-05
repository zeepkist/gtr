namespace TNRD.Zeepkist.GTR.LevelBrowser;

/// <summary>
/// Builds the default <c>levelItems</c> browse arguments from raw discovery inputs: always-on
/// hygiene (applied by the query object), case-insensitive substring matchers for name and author
/// when non-empty, <c>DATE_CREATED_DESC</c> order, and <c>first</c>/<c>offset</c> pagination.
/// </summary>
public static class LevelItemsBrowseQueryBuilder
{
    /// <summary>Default number of rows per page.</summary>
    public const int DefaultPageSize = 12;

    /// <summary>
    /// Produces the resolved <see cref="LevelItemsBrowseQuery"/>. Blank/whitespace filter text
    /// becomes <c>null</c> (no predicate); page and pageSize are clamped to sane minimums.
    /// </summary>
    public static LevelItemsBrowseQuery Build(
        string name,
        string fileAuthor,
        int page = 0,
        int pageSize = DefaultPageSize)
    {
        if (page < 0)
            page = 0;
        if (pageSize < 1)
            pageSize = DefaultPageSize;

        return new LevelItemsBrowseQuery(
            Normalize(name),
            Normalize(fileAuthor),
            pageSize,
            page * pageSize);
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }
}

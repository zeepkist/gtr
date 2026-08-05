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

    /// <summary>Explicit default order; there is no sort UI in v1.</summary>
    public const string OrderBy = "DATE_CREATED_DESC";

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

    /// <summary>Page size (<c>first</c>), following the existing <c>first</c>/<c>offset</c> paging style.</summary>
    public int First { get; }

    /// <summary>Row offset (<c>offset</c>) = page * pageSize.</summary>
    public int Offset { get; }

    internal LevelItemsBrowseQuery(
        string nameIncludesInsensitive,
        string fileAuthorIncludesInsensitive,
        int first,
        int offset)
    {
        NameIncludesInsensitive = nameIncludesInsensitive;
        FileAuthorIncludesInsensitive = fileAuthorIncludesInsensitive;
        First = first;
        Offset = offset;
    }
}

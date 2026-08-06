using System.Collections.Generic;

namespace TNRD.Zeepkist.GTR.LevelBrowser;

/// <summary>
/// A successful page of browse results: the rows for the requested page plus the catalog-wide
/// <see cref="TotalCount"/> so a host/UI can decide whether a next page exists.
/// </summary>
public sealed class LevelBrowsePage
{
    public LevelBrowsePage(IReadOnlyList<LevelBrowseRow> rows, int totalCount)
    {
        Rows = rows;
        TotalCount = totalCount;
    }

    public IReadOnlyList<LevelBrowseRow> Rows { get; }

    /// <summary>Count of all matching rows across every page.</summary>
    public int TotalCount { get; }

    public bool IsEmpty => Rows.Count == 0;

    /// <summary>True when rows beyond this page's <paramref name="offset"/> remain to be fetched.</summary>
    public bool HasMoreAfter(int offset)
    {
        return offset + Rows.Count < TotalCount;
    }
}

using System.Collections.Generic;

namespace TNRD.Zeepkist.GTR.LevelBrowser;

/// <summary>
/// Builds the short numbered-page slot list for the Level Browser pager: first and last page,
/// the current page and its immediate neighbors, with <see cref="Ellipsis"/> where gaps remain.
/// </summary>
public static class LevelBrowserPageWindow
{
    /// <summary>Sentinel value meaning "render an ellipsis" rather than a page button.</summary>
    public const int Ellipsis = -1;

    /// <summary>
    /// Returns zero-based page indices (and <see cref="Ellipsis"/> gaps) for a sliding window
    /// around <paramref name="currentPage"/> within <paramref name="totalPages"/>.
    /// </summary>
    public static IReadOnlyList<int> Build(int currentPage, int totalPages)
    {
        if (totalPages <= 1)
            return new[] { 0 };

        if (currentPage < 0)
            currentPage = 0;
        if (currentPage >= totalPages)
            currentPage = totalPages - 1;

        var pages = new SortedSet<int>
        {
            0,
            Clamp(currentPage - 1, totalPages),
            currentPage,
            Clamp(currentPage + 1, totalPages),
            totalPages - 1
        };

        var slots = new List<int>(pages.Count * 2);
        int previous = -1;
        foreach (int page in pages)
        {
            if (previous >= 0 && page - previous > 1)
                slots.Add(Ellipsis);
            slots.Add(page);
            previous = page;
        }

        return slots;
    }

    private static int Clamp(int page, int totalPages)
    {
        if (page < 0)
            return 0;
        if (page >= totalPages)
            return totalPages - 1;
        return page;
    }
}

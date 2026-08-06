using System.Globalization;

namespace TNRD.Zeepkist.GTR.LevelBrowser;

/// <summary>
/// Parses a GraphQL <c>BigInt</c> workshop id (delivered as a string by the client) into the
/// <c>ulong</c> the game's <c>OnlineZeeplevel.WorkshopID</c> uses. Tolerant: unparseable or empty
/// values become <c>0</c> (the adventure / non-workshop sentinel).
/// </summary>
public static class WorkshopIdParser
{
    public static ulong Parse(string workshopId)
    {
        if (string.IsNullOrWhiteSpace(workshopId))
            return 0UL;

        return ulong.TryParse(workshopId.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong value)
            ? value
            : 0UL;
    }
}

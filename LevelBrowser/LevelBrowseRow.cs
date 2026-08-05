namespace TNRD.Zeepkist.GTR.LevelBrowser;

/// <summary>
/// One discovery row from the <c>levelItems</c> catalog: the fields needed to build a
/// <see cref="LevelBrowserSelection"/> plus the display-only <see cref="ImageUrl"/> thumbnail.
/// </summary>
public sealed class LevelBrowseRow
{
    public LevelBrowseRow(string fileUid, ulong workshopId, string name, string fileAuthor, string imageUrl)
    {
        FileUid = fileUid;
        WorkshopId = workshopId;
        Name = name;
        FileAuthor = fileAuthor;
        ImageUrl = imageUrl;
    }

    public string FileUid { get; }
    public ulong WorkshopId { get; }
    public string Name { get; }
    public string FileAuthor { get; }

    /// <summary>Display-only thumbnail URL; not part of the Level Browser Selection.</summary>
    public string ImageUrl { get; }

    /// <summary>True when a thumbnail URL is present.</summary>
    public bool HasThumbnail => !string.IsNullOrEmpty(ImageUrl);

    /// <summary>Projects the identity/display fields into the host-facing Selection.</summary>
    public LevelBrowserSelection ToSelection()
    {
        return new LevelBrowserSelection(FileUid, WorkshopId, Name, FileAuthor);
    }
}

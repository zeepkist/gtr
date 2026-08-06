using TNRD.Zeepkist.GTR.LevelBrowser;

namespace TNRD.Zeepkist.GTR.PlaylistBrowserHost;

/// <summary>
/// Maps a <see cref="LevelBrowserSelection"/> to <see cref="PlaylistLevelMetadata"/> using the same
/// field shape as a vanilla playlist add (<c>fileUid</c>→UID, <c>workshopId</c>→WorkshopID,
/// <c>name</c>→Name, <c>fileAuthor</c>→Author). Collaborators and override author are left empty by
/// design — the browse catalog does not carry them.
/// </summary>
public static class PlaylistLevelMapper
{
    public static PlaylistLevelMetadata ToPlaylistMetadata(LevelBrowserSelection selection)
    {
        return new PlaylistLevelMetadata(
            selection.FileUid,
            selection.WorkshopId,
            selection.Name,
            selection.FileAuthor,
            string.Empty,
            string.Empty);
    }
}

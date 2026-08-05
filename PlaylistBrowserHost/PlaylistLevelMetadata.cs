namespace TNRD.Zeepkist.GTR.PlaylistBrowserHost;

/// <summary>
/// The metadata fields the Playlist Browser Host copies into a game <c>OnlineZeeplevel</c> when
/// adding a browsed level to the draft Lobby Playlist. Pure so the Selection → playlist mapping is
/// testable without the Unity game type.
/// </summary>
public sealed record PlaylistLevelMetadata(
    string Uid,
    ulong WorkshopId,
    string Name,
    string Author,
    string Collaborators,
    string OverrideAuthorName);

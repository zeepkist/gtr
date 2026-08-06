namespace TNRD.Zeepkist.GTR.LevelBrowser;

/// <summary>
/// The LevelItem-shaped payload the Level Browser hands a Browser Host on a confirmed pick.
/// Carries identity and display fields only — never a playlist or download action.
/// </summary>
public sealed record LevelBrowserSelection(string FileUid, ulong WorkshopId, string Name, string FileAuthor);

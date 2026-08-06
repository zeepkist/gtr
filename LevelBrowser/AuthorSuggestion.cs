namespace TNRD.Zeepkist.GTR.LevelBrowser;

/// <summary>
/// A user suggestion for the Level Browser "Uploaded by" autocomplete: the Steam id used as
/// <c>authorId</c> and the display <c>steamName</c>.
/// </summary>
public sealed class AuthorSuggestion
{
    public AuthorSuggestion(string steamId, string steamName)
    {
        SteamId = steamId;
        SteamName = steamName;
    }

    public string SteamId { get; }

    public string SteamName { get; }
}

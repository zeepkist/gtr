namespace TNRD.Zeepkist.GTR.LevelBrowser;

/// <summary>
/// When true, the Playlist Manager's <c>GenericNavigator</c> should ignore Cancel/Backspace (and
/// other nav input) so Imui text fields in the Level Browser can receive them. Set each draw while
/// a filter TextEdit is focused; cleared when the browser is closed or no field is focused.
/// </summary>
public static class LevelBrowserInputGate
{
    public static bool SuppressNavigatorInput { get; set; }
}

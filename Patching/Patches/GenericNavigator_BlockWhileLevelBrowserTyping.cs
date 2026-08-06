using HarmonyLib;
using TNRD.Zeepkist.GTR.LevelBrowser;

namespace TNRD.Zeepkist.GTR.Patching.Patches;

/// <summary>
/// While a Level Browser filter text field is focused, skip <see cref="GenericNavigator"/> input
/// so Cancel/Backspace does not close the Playlist Manager (Backspace is bound to MenuCancel).
/// Mirrors the game's own <c>lockForInput</c> behavior used for TMP input fields.
/// </summary>
[HarmonyPatch(typeof(GenericNavigator), "Update")]
public static class GenericNavigator_BlockWhileLevelBrowserTyping
{
    private static bool Prefix()
    {
        return !LevelBrowserInputGate.SuppressNavigatorInput;
    }
}

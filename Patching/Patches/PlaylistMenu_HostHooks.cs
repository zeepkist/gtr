using System;
using HarmonyLib;

namespace TNRD.Zeepkist.GTR.Patching.Patches;

/// <summary>
/// Lifecycle hooks the Playlist Browser Host needs from the game's <see cref="PlaylistMenu"/>: when the
/// menu opens/closes and when the user leaves the home screen for a sub-menu (add level / load
/// playlist). The host uses these to inject its "Browse levels" button and to open/close the Level
/// Browser at the right moments.
/// </summary>
public static class PlaylistMenu_HostHooks
{
    /// <summary>Playlist Manager home opened; carries the live menu instance.</summary>
    public static event Action<PlaylistMenu> Opened;

    /// <summary>Playlist Manager closed (Accept, quit, or menu teardown).</summary>
    public static event Action<PlaylistMenu> Closed;

    /// <summary>User left the home action row for a sub-menu (add level / load playlist).</summary>
    public static event Action LeftHome;

    [HarmonyPatch(typeof(PlaylistMenu), nameof(PlaylistMenu.OnOpen))]
    public class OnOpen
    {
        private static void Postfix(PlaylistMenu __instance) => Opened?.Invoke(__instance);
    }

    [HarmonyPatch(typeof(PlaylistMenu), nameof(PlaylistMenu.OnClose))]
    public class OnClose
    {
        private static void Postfix(PlaylistMenu __instance) => Closed?.Invoke(__instance);
    }

    [HarmonyPatch(typeof(PlaylistMenu), nameof(PlaylistMenu.GoFromHomeToAddLevelMenu))]
    public class GoToAddLevel
    {
        private static void Postfix() => LeftHome?.Invoke();
    }

    [HarmonyPatch(typeof(PlaylistMenu), nameof(PlaylistMenu.GoFromHomeToLoadPlaylistMenu))]
    public class GoToLoadPlaylist
    {
        private static void Postfix() => LeftHome?.Invoke();
    }
}

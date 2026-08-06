using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.LevelBrowser;
using TNRD.Zeepkist.GTR.Patching.Patches;
using UnityEngine;
using UnityEngine.Events;
using ZeepkistNetworking;

namespace TNRD.Zeepkist.GTR.PlaylistBrowserHost;

/// <summary>
/// The first Browser Host: wires the reusable Level Browser to the game's Playlist Manager. It injects
/// a "Browse levels" button after Add level on the playlist home, toggles the shared
/// <see cref="LevelBrowserSession"/> open/closed, and on each confirmed pick appends the selected level
/// to the open draft playlist (<c>Add</c> + <c>RefreshPlaylist</c>, no download, no sync packet) and
/// pushes the refreshed already-in UIDs back to the session. Accept still syncs the Lobby Playlist via
/// the vanilla path; leaving home / closing the menu closes the browser.
/// </summary>
public sealed class PlaylistBrowserHostService : IEagerService, IDisposable
{
    private const string BrowseButtonName = "GTR_BrowseLevels";
    private const string BrowseButtonLabel = "Browse levels";

    private readonly LevelBrowserSession _session;

    private PlaylistMenu _playlistMenu;

    public PlaylistBrowserHostService(LevelBrowserSession session)
    {
        _session = session;

        PlaylistMenu_HostHooks.Opened += OnPlaylistOpened;
        PlaylistMenu_HostHooks.Closed += OnPlaylistClosed;
        PlaylistMenu_HostHooks.LeftHome += OnLeftHome;
    }

    private void OnPlaylistOpened(PlaylistMenu menu)
    {
        _playlistMenu = menu;
        _session.CloseByHost();
        EnsureBrowseButton(menu);
    }

    private void OnPlaylistClosed(PlaylistMenu menu)
    {
        _session.CloseByHost();
    }

    private void OnLeftHome()
    {
        _session.CloseByHost();
    }

    private void Toggle()
    {
        if (_session.IsOpen)
        {
            _session.CloseByHost();
            return;
        }

        if (_playlistMenu == null)
            return;

        _session.Open(AddSelection, CurrentUids());
    }

    private IEnumerable<string> CurrentUids()
    {
        return _playlistMenu?.thePlaylist == null
            ? Enumerable.Empty<string>()
            : _playlistMenu.thePlaylist.Select(level => level.UID);
    }

    private void AddSelection(LevelBrowserSelection selection)
    {
        if (_playlistMenu?.thePlaylist == null)
            return;

        PlaylistLevelMetadata metadata = PlaylistLevelMapper.ToPlaylistMetadata(selection);

        _playlistMenu.thePlaylist.Add(new OnlineZeeplevel
        {
            UID = metadata.Uid,
            WorkshopID = metadata.WorkshopId,
            Name = metadata.Name,
            Author = metadata.Author,
            Collaborators = metadata.Collaborators,
            OverrideAuthorName = metadata.OverrideAuthorName
        });

        _playlistMenu.RefreshPlaylist();
        _session.UpdateAlreadyIn(CurrentUids());
    }

    private void EnsureBrowseButton(PlaylistMenu menu)
    {
        GenericButton template = menu.addNewLevelButton;
        if (template == null)
            return;

        Transform parent = template.transform.parent;

        Transform existing = parent.Find(BrowseButtonName);
        if (existing != null)
        {
            GenericButton existingButton = existing.GetComponent<GenericButton>();
            if (existingButton != null)
                existingButton.currentNavigator = menu.navigator;
            return;
        }

        GameObject clone = UnityEngine.Object.Instantiate(template.gameObject, parent);
        clone.name = BrowseButtonName;
        clone.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);

        GenericButton browse = clone.GetComponent<GenericButton>();
        if (browse == null)
        {
            UnityEngine.Object.Destroy(clone);
            return;
        }

        ApplyLabel(clone, BrowseButtonLabel);

        // Drop the cloned Add-level wiring and install our toggle.
        browse.onClick = new UnityEvent();
        browse.onHover = new UnityEvent();
        browse.onLeft = new UnityEvent();
        browse.onRight = new UnityEvent();
        browse.onSliderEnd = new UnityEvent();
        browse.onClick.AddListener(Toggle);
        browse.currentNavigator = menu.navigator;

        WireNavigation(template, browse);

        BrowseLevelsDownloadGuard guard = clone.AddComponent<BrowseLevelsDownloadGuard>();
        guard.button = browse;
    }

    private static void ApplyLabel(GameObject clone, string label)
    {
        // Some button labels are driven by a localization component that would overwrite our text.
        foreach (MonoBehaviour behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour != null && behaviour.GetType().Name.IndexOf("Localize", StringComparison.Ordinal) >= 0)
                UnityEngine.Object.Destroy(behaviour);
        }

        foreach (TMP_Text text in clone.GetComponentsInChildren<TMP_Text>(true))
            text.text = label;
    }

    private static void WireNavigation(GenericButton template, GenericButton browse)
    {
        GenericButton oldRight = template.right;

        browse.left = template;
        browse.right = oldRight;
        browse.up = template.up;
        browse.down = template.down;

        template.right = browse;
        if (oldRight != null)
            oldRight.left = browse;
    }

    public void Dispose()
    {
        PlaylistMenu_HostHooks.Opened -= OnPlaylistOpened;
        PlaylistMenu_HostHooks.Closed -= OnPlaylistClosed;
        PlaylistMenu_HostHooks.LeftHome -= OnLeftHome;
    }
}

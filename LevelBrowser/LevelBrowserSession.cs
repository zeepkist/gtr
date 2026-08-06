using System;
using System.Collections.Generic;

namespace TNRD.Zeepkist.GTR.LevelBrowser;

/// <summary>
/// The pure Level Browser ↔ Browser Host session. Holds the host callback, the host-supplied
/// set of already-in level UIDs, and the discovery UI state (search text, filters, page). It knows
/// nothing about playlists, Unity, Imui, or GraphQL — a host opens it, receives one
/// <see cref="LevelBrowserSelection"/> per confirmed pick, and can push fresh already-in UIDs.
/// </summary>
public sealed class LevelBrowserSession
{
    private readonly HashSet<string> _alreadyInUids = new(StringComparer.Ordinal);

    private Action<LevelBrowserSelection> _onSelected;
    private Action _onClosed;

    /// <summary>True while a host session is active.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Discovery filter: level name substring. Reset when a new session opens.</summary>
    public string SearchName { get; set; } = string.Empty;

    /// <summary>Discovery filter: file-author substring. Reset when a new session opens.</summary>
    public string SearchAuthor { get; set; } = string.Empty;

    /// <summary>Sort order. Reset when a new session opens.</summary>
    public LevelBrowseSort Sort { get; set; }

    /// <summary>Date-created preset. Reset when a new session opens.</summary>
    public LevelBrowseDateRange DateRange { get; set; }

    /// <summary>Track-length preset. Reset when a new session opens.</summary>
    public LevelBrowseTrackLength TrackLength { get; set; }

    /// <summary>Vote-quality rating preset. Reset when a new session opens.</summary>
    public LevelBrowseRating Rating { get; set; }

    /// <summary>Zero-based results page. Reset when a new session opens.</summary>
    public int Page { get; set; }

    /// <summary>
    /// Opens (or, when already open, replaces) the session. Replacing installs the new callback and
    /// already-in set and resets discovery UI state (search text, filters, page). Opening never
    /// notifies a previous host of a close — a rebind is not a user close.
    /// </summary>
    /// <param name="onSelected">Invoked once per confirmed pick with the selected row.</param>
    /// <param name="alreadyInUids">Level UIDs the host already owns; matching rows mark as in-use.</param>
    /// <param name="onClosed">Optional: invoked when the user closes via window chrome.</param>
    public void Open(
        Action<LevelBrowserSelection> onSelected,
        IEnumerable<string> alreadyInUids = null,
        Action onClosed = null)
    {
        _onSelected = onSelected ?? throw new ArgumentNullException(nameof(onSelected));
        _onClosed = onClosed;

        ReplaceAlreadyIn(alreadyInUids);
        ResetDiscoveryState();

        IsOpen = true;
    }

    /// <summary>
    /// Replaces the set of already-in level UIDs on the live session (e.g. the host pushes this
    /// after each successful add). No-op when the session is closed.
    /// </summary>
    public void UpdateAlreadyIn(IEnumerable<string> alreadyInUids)
    {
        if (!IsOpen)
            return;

        ReplaceAlreadyIn(alreadyInUids);
    }

    /// <summary>True when the given level UID is in the host-supplied already-in set.</summary>
    public bool IsAlreadyIn(string fileUid)
    {
        return !string.IsNullOrEmpty(fileUid) && _alreadyInUids.Contains(fileUid);
    }

    /// <summary>
    /// Attempts to select a browse row. A free row emits exactly one Level Browser Selection to the
    /// host callback and returns <see cref="SelectionOutcome.Selected"/>. An already-in row emits
    /// nothing (host shows a toast) and returns <see cref="SelectionOutcome.AlreadyIn"/>.
    /// </summary>
    public SelectionOutcome TrySelect(LevelBrowserSelection selection)
    {
        if (selection == null)
            throw new ArgumentNullException(nameof(selection));

        if (!IsOpen)
            return SelectionOutcome.NotOpen;

        if (IsAlreadyIn(selection.FileUid))
            return SelectionOutcome.AlreadyIn;

        _onSelected?.Invoke(selection);
        return SelectionOutcome.Selected;
    }

    /// <summary>
    /// Closes the session because the user dismissed the browser (window chrome). Notifies the host
    /// via the close callback. Idempotent: closing an already-closed session does nothing.
    /// </summary>
    public void CloseByUser()
    {
        if (!IsOpen)
            return;

        Action onClosed = _onClosed;
        EndSession();
        onClosed?.Invoke();
    }

    /// <summary>
    /// Closes the session because the host tore down (e.g. Playlist Manager lifecycle ended). Does
    /// not notify the host back — it already knows. Idempotent.
    /// </summary>
    public void CloseByHost()
    {
        if (!IsOpen)
            return;

        EndSession();
    }

    private void EndSession()
    {
        IsOpen = false;
        _onSelected = null;
        _onClosed = null;
        _alreadyInUids.Clear();
        ResetDiscoveryState();
    }

    private void ResetDiscoveryState()
    {
        SearchName = string.Empty;
        SearchAuthor = string.Empty;
        Sort = LevelBrowseSort.Newest;
        DateRange = LevelBrowseDateRange.AnyTime;
        TrackLength = LevelBrowseTrackLength.Any;
        Rating = LevelBrowseRating.Any;
        Page = 0;
    }

    private void ReplaceAlreadyIn(IEnumerable<string> alreadyInUids)
    {
        _alreadyInUids.Clear();
        if (alreadyInUids == null)
            return;

        foreach (string uid in alreadyInUids)
        {
            if (!string.IsNullOrEmpty(uid))
                _alreadyInUids.Add(uid);
        }
    }
}

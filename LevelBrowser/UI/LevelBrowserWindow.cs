using System;
using System.Collections.Generic;
using System.Threading;
using Imui.Controls;
using Imui.Core;
using Imui.Rendering;
using Steamworks;
using TNRD.Zeepkist.GTR.UI;
using UnityEngine;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;
using ZeepSDK.UI;

namespace TNRD.Zeepkist.GTR.LevelBrowser.UI;

/// <summary>
/// The Level Browser Imui window (layout B): a left filter rail and a right results area with
/// thumbnail cards, pagination below, and shared loading / empty / error+Retry states.
/// Driven entirely by the <see cref="LevelBrowserSession"/> (open state, filters, page, already-in
/// marking, Selection delivery) and the <see cref="LevelBrowseService"/> (catalog fetch).
/// </summary>
public sealed class LevelBrowserWindow : IZeepGUIDrawer
{
    private const string WindowTitle = "Level Browser";
    private const float DefaultWidth = 960f;
    private const float DefaultHeight = 720f;
    private const float RailWidth = 188f;
    private const float Gap = 8f;
    private const float CardHeight = 62f;
    private const float CardSpacing = 6f;
    private const float ThumbWidth = 96f;
    private const float ActionsWidth = 112f;
    private const float ButtonHeight = 28f;
    private const int PageSize = LevelItemsBrowseQueryBuilder.DefaultPageSize;
    private const float DebounceSeconds = 0.3f;
    private const float ToastSeconds = 2.5f;

    private static readonly string[] SortLabels =
    {
        "Newest",
        "Oldest",
        "Name A–Z",
        "Name Z–A",
        "Shortest",
        "Longest"
    };

    private static readonly string[] DateRangeLabels =
    {
        "Any time",
        "Past week",
        "Past month",
        "Past year"
    };

    private static readonly string[] TrackLengthLabels =
    {
        "Any",
        "Short (<30s)",
        "Medium (30–90s)",
        "Long (≥90s)"
    };

    private static readonly string[] RatingLabels =
    {
        "Any",
        "Well-rated",
        "Top-rated"
    };

    private enum ResultsState
    {
        Loading,
        Loaded,
        Error
    }

    private readonly LevelBrowserSession _session;
    private readonly LevelBrowseService _service;
    private readonly LevelThumbnailCache _thumbnails;

    private bool _wasOpen;
    private bool _windowOpen;
    private bool _mouseOver;

    private ResultsState _state = ResultsState.Loading;
    private IReadOnlyList<LevelBrowseRow> _rows = Array.Empty<LevelBrowseRow>();
    private int _totalCount;
    private int _appliedOffset;
    private int _requestId;
    private CancellationTokenSource _cts;

    private float _fetchAt = -1f;
    private string _appliedName = "\uffff";
    private string _appliedAuthor = "\uffff";
    private string _appliedAuthorUserId = "\uffff";
    private LevelBrowseSort _appliedSort = (LevelBrowseSort)(-1);
    private LevelBrowseDateRange _appliedDateRange = (LevelBrowseDateRange)(-1);
    private LevelBrowseTrackLength _appliedTrackLength = (LevelBrowseTrackLength)(-1);
    private LevelBrowseRating _appliedRating = (LevelBrowseRating)(-1);
    private bool _appliedOwnLevels = true;
    private bool _appliedWithoutMyPb = true;
    private bool _appliedWithoutRecords = true;
    private int _appliedPage = -1;

    private string _authorTyped = string.Empty;
    private string _authorLookupQuery = "\uffff";
    private IReadOnlyList<AuthorSuggestion> _authorSuggestions = Array.Empty<AuthorSuggestion>();
    private float _authorLookupAt = -1f;
    private int _authorRequestId;
    private CancellationTokenSource _authorCts;
    private AuthorSuggestion _pendingAuthorPick;
    private bool _authorListOpen;

    private string _toast;
    private float _toastUntil;

    public LevelBrowserWindow(
        LevelBrowserSession session,
        LevelBrowseService service,
        LevelThumbnailCache thumbnails)
    {
        _session = session;
        _service = service;
        _thumbnails = thumbnails;
    }

    public void OnZeepGUI(ImGui gui)
    {
        if (!_session.IsOpen)
        {
            LevelBrowserInputGate.SuppressNavigatorInput = false;
            _wasOpen = false;
            return;
        }

        // Cleared each draw; set again below if a filter TextEdit is focused. Navigator.Update reads
        // the previous frame's value when it runs before Imui, which is enough to keep Backspace
        // from closing the Playlist Manager while typing.
        LevelBrowserInputGate.SuppressNavigatorInput = false;

        float now = Time.unscaledTime;

        if (!_wasOpen)
            OnOpened(now);
        _wasOpen = true;

        _thumbnails.Poll();
        MaybeRefetch(now);
        MaybeAuthorLookup(now);

        _windowOpen = true;
        ImRect rect = ImWindowPlacement.GetRect(
            gui, WindowTitle.AsSpan(), DefaultWidth, DefaultHeight, ImWindowAnchor.MiddleCenter);

        if (gui.BeginWindow(WindowTitle, ref _windowOpen, ref _mouseOver, rect, ImWindowFlag.None))
        {
            try
            {
                DrawBody(gui, now);
            }
            finally
            {
                gui.EndWindow();
            }
        }

        if (!_windowOpen)
            _session.CloseByUser();
    }

    private void OnOpened(float now)
    {
        _state = ResultsState.Loading;
        _rows = Array.Empty<LevelBrowseRow>();
        _totalCount = 0;
        _appliedOffset = 0;
        _appliedName = "\uffff";
        _appliedAuthor = "\uffff";
        _appliedAuthorUserId = "\uffff";
        _appliedSort = (LevelBrowseSort)(-1);
        _appliedDateRange = (LevelBrowseDateRange)(-1);
        _appliedTrackLength = (LevelBrowseTrackLength)(-1);
        _appliedRating = (LevelBrowseRating)(-1);
        _appliedOwnLevels = true;
        _appliedWithoutMyPb = true;
        _appliedWithoutRecords = true;
        _appliedPage = -1;
        _authorTyped = string.Empty;
        _authorLookupQuery = "\uffff";
        _authorSuggestions = Array.Empty<AuthorSuggestion>();
        _authorLookupAt = -1f;
        _pendingAuthorPick = null;
        _authorListOpen = false;
        _authorCts?.Cancel();
        _authorCts?.Dispose();
        _authorCts = null;
        _toast = null;
        _fetchAt = now;

        // Warm the author-name cache so Uploaded-by filtering is local after the first load.
        _service.EnsureAuthorCacheAsync().Forget();
    }

    private void MaybeRefetch(float now)
    {
        if (_fetchAt >= 0f && now >= _fetchAt)
        {
            _fetchAt = -1f;
            BeginFetch();
            return;
        }

        // Fallback: pick up external session state changes (e.g. a rebind that reset filters/page).
        if (_fetchAt < 0f && _state != ResultsState.Loading && HasPendingFilterChange())
            _fetchAt = now;
    }

    private bool HasPendingFilterChange()
    {
        return !string.Equals(_session.SearchName ?? string.Empty, _appliedName, StringComparison.Ordinal) ||
               !string.Equals(_session.SearchAuthor ?? string.Empty, _appliedAuthor, StringComparison.Ordinal) ||
               !string.Equals(_session.AuthorUserId ?? string.Empty, _appliedAuthorUserId, StringComparison.Ordinal) ||
               _session.Sort != _appliedSort ||
               _session.DateRange != _appliedDateRange ||
               _session.TrackLength != _appliedTrackLength ||
               _session.Rating != _appliedRating ||
               _session.OwnLevelsOnly != _appliedOwnLevels ||
               _session.WithoutMyPersonalBest != _appliedWithoutMyPb ||
               _session.WithoutRecords != _appliedWithoutRecords ||
               _session.Page != _appliedPage;
    }

    private void BeginFetch()
    {
        string name = _session.SearchName ?? string.Empty;
        string author = _session.SearchAuthor ?? string.Empty;
        string authorUserId = _session.AuthorUserId ?? string.Empty;
        LevelBrowseSort sort = _session.Sort;
        LevelBrowseDateRange dateRange = _session.DateRange;
        LevelBrowseTrackLength trackLength = _session.TrackLength;
        LevelBrowseRating rating = _session.Rating;
        bool ownLevelsOnly = _session.OwnLevelsOnly;
        bool withoutMyPersonalBest = _session.WithoutMyPersonalBest;
        bool withoutRecords = _session.WithoutRecords;
        int page = _session.Page < 0 ? 0 : _session.Page;
        string ownerSteamId = SteamClient.SteamId.Value.ToString();

        _appliedName = name;
        _appliedAuthor = author;
        _appliedAuthorUserId = authorUserId;
        _appliedSort = sort;
        _appliedDateRange = dateRange;
        _appliedTrackLength = trackLength;
        _appliedRating = rating;
        _appliedOwnLevels = ownLevelsOnly;
        _appliedWithoutMyPb = withoutMyPersonalBest;
        _appliedWithoutRecords = withoutRecords;
        _appliedPage = page;
        _appliedOffset = page * PageSize;

        _state = ResultsState.Loading;
        _rows = Array.Empty<LevelBrowseRow>();

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        _requestId++;
        FetchAsync(
                _requestId,
                name,
                author,
                authorUserId,
                page,
                sort,
                dateRange,
                trackLength,
                rating,
                ownLevelsOnly,
                withoutMyPersonalBest,
                withoutRecords,
                ownerSteamId,
                _cts.Token)
            .Forget();
    }

    private async UniTaskVoid FetchAsync(
        int requestId,
        string name,
        string author,
        string authorUserId,
        int page,
        LevelBrowseSort sort,
        LevelBrowseDateRange dateRange,
        LevelBrowseTrackLength trackLength,
        LevelBrowseRating rating,
        bool ownLevelsOnly,
        bool withoutMyPersonalBest,
        bool withoutRecords,
        string ownerSteamId,
        CancellationToken ct)
    {
        Result<LevelBrowsePage> result = await _service.BrowseAsync(
            name,
            author,
            page,
            sort,
            dateRange,
            trackLength,
            rating,
            ownLevelsOnly,
            withoutMyPersonalBest,
            withoutRecords,
            ownerSteamId,
            authorUserId,
            ct);

        if (requestId != _requestId)
            return;

        if (result.IsFailed)
        {
            _rows = Array.Empty<LevelBrowseRow>();
            _totalCount = 0;
            _state = ResultsState.Error;
            return;
        }

        _rows = result.Value.Rows;
        _totalCount = result.Value.TotalCount;
        _state = ResultsState.Loaded;
    }

    private void MaybeAuthorLookup(float now)
    {
        if (_authorLookupAt >= 0f && now >= _authorLookupAt)
        {
            _authorLookupAt = -1f;
            BeginAuthorLookup();
        }
    }

    private void BeginAuthorLookup()
    {
        string query = _authorTyped?.Trim() ?? string.Empty;
        if (query.Length < 2)
        {
            _authorLookupQuery = query;
            _authorSuggestions = Array.Empty<AuthorSuggestion>();
            _authorCts?.Cancel();
            return;
        }

        if (string.Equals(query, _authorLookupQuery, StringComparison.Ordinal))
            return;

        _authorLookupQuery = query;

        // Fast path: filter the warm in-memory cache without an async hop.
        if (_service.TryFilterAuthors(query, LevelBrowseService.DefaultAuthorSearchLimit, out IReadOnlyList<AuthorSuggestion> local))
        {
            _authorSuggestions = local;
            return;
        }

        _authorCts?.Cancel();
        _authorCts?.Dispose();
        _authorCts = new CancellationTokenSource();

        _authorRequestId++;
        SearchAuthorsAsync(_authorRequestId, query, _authorCts.Token).Forget();
    }

    private async UniTaskVoid SearchAuthorsAsync(int requestId, string query, CancellationToken ct)
    {
        Result<IReadOnlyList<AuthorSuggestion>> result = await _service.SearchAuthorsAsync(query, ct: ct);
        if (requestId != _authorRequestId)
            return;

        if (result.IsFailed)
        {
            _authorSuggestions = Array.Empty<AuthorSuggestion>();
            return;
        }

        _authorSuggestions = result.Value;
    }

    private void DrawBody(ImGui gui, float now)
    {
        ImRect area = gui.AddLayoutRect(gui.GetLayoutSize());

        var rail = new ImRect(area.X, area.Y, RailWidth, area.H);
        var main = new ImRect(area.X + RailWidth + Gap, area.Y, area.W - RailWidth - Gap, area.H);

        DrawRail(gui, rail, now);

        float pagerHeight = gui.GetRowHeight();
        var pager = new ImRect(main.X, main.Y, main.W, pagerHeight);
        var results = new ImRect(main.X, main.Y + pagerHeight + Gap, main.W, main.H - pagerHeight - Gap);

        DrawResults(gui, results, now);
        DrawPager(gui, pager, now);
        DrawToast(gui, area, now);
    }

    private void DrawRail(ImGui gui, ImRect rail, float now)
    {
        float footerHeight = gui.GetRowHeight();
        var footer = new ImRect(rail.X, rail.Y, rail.W, footerHeight);
        var filters = new ImRect(
            rail.X,
            rail.Y + footerHeight + Gap,
            rail.W,
            Mathf.Max(0f, rail.H - footerHeight - Gap));

        DrawRailFilters(gui, filters, now);
        DrawRailFooter(gui, footer);
    }

    private void DrawRailFilters(ImGui gui, ImRect filters, float now)
    {
        gui.Layout.Push(ImAxis.Vertical, filters);
        gui.Canvas.PushClipRect(filters);
        gui.BeginScrollable();

        try
        {
            string name = _session.SearchName ?? string.Empty;
            gui.Text("Name".AsSpan(), gui.Style.Text.Color);
            if (DrawFilterTextEdit(gui, ref name, "includes…".AsSpan()))
            {
                _session.SearchName = name;
                OnFilterChanged(now);
            }

            string author = _session.SearchAuthor ?? string.Empty;
            gui.Text("Author".AsSpan(), gui.Style.Text.Color);
            if (DrawFilterTextEdit(gui, ref author, "fileAuthor…".AsSpan()))
            {
                _session.SearchAuthor = author;
                OnFilterChanged(now);
            }

            DrawUploadedBy(gui, now);

            DrawRailDropdown(gui, "Sort", SortLabels, (int)_session.Sort, now, i => _session.Sort = (LevelBrowseSort)i);
            DrawRailDropdown(gui, "Created", DateRangeLabels, (int)_session.DateRange, now, i => _session.DateRange = (LevelBrowseDateRange)i);
            DrawRailDropdown(gui, "Length", TrackLengthLabels, (int)_session.TrackLength, now, i => _session.TrackLength = (LevelBrowseTrackLength)i);
            DrawRailDropdown(gui, "Rating", RatingLabels, (int)_session.Rating, now, i => _session.Rating = (LevelBrowseRating)i);

            bool hasAuthorUser = !string.IsNullOrEmpty(_session.AuthorUserId);
            DrawRailCheckbox(
                gui,
                "My levels",
                _session.OwnLevelsOnly,
                now,
                v =>
                {
                    _session.OwnLevelsOnly = v;
                    if (v)
                        ClearAuthorUserSelection(scheduleLookup: false, now: now);
                },
                enabled: !hasAuthorUser);
            DrawRailCheckbox(gui, "No PB", _session.WithoutMyPersonalBest, now, v => _session.WithoutMyPersonalBest = v);
            DrawRailCheckbox(gui, "No WR", _session.WithoutRecords, now, v => _session.WithoutRecords = v);
        }
        finally
        {
            gui.EndScrollable();
            gui.Canvas.PopClipRect();
            gui.Layout.Pop();
        }
    }

    private void DrawRailFooter(ImGui gui, ImRect footer)
    {
        if (_state != ResultsState.Loaded)
            return;

        string info = $"{_totalCount} levels";
        var settings = new ImTextSettings(gui.Style.Layout.TextSize * 0.85f, 0.5f, 0.5f);
        gui.Canvas.Text(info.AsSpan(), gui.Style.TextEdit.HintFrontColor, footer, in settings);
    }

    private void DrawUploadedBy(ImGui gui, float now)
    {
        // Apply picks before TextEdit so the field shows the chosen name this frame.
        if (_pendingAuthorPick != null)
        {
            AuthorSuggestion pick = _pendingAuthorPick;
            _pendingAuthorPick = null;
            ApplyAuthorPick(pick, now);
        }

        gui.Text("Uploaded by".AsSpan(), gui.Style.Text.Color);

        bool ownLevelsOnly = _session.OwnLevelsOnly;
        if (ownLevelsOnly)
            gui.BeginReadOnly(true);

        uint fieldId = 0;
        ImRect fieldRect = default;

        try
        {
            // Restore display text if we have a selection but the field was cleared externally.
            if (!string.IsNullOrEmpty(_session.AuthorUserId) &&
                string.IsNullOrEmpty(_authorTyped) &&
                !string.IsNullOrEmpty(_session.AuthorUserName))
            {
                _authorTyped = _session.AuthorUserName;
            }

            gui.AddSpacingIfLayoutFrameNotEmpty();
            fieldId = gui.GetNextControlId();
            fieldRect = ImTextEdit.AddRect(gui, default, multiline: false, out _);
            ref ImTextEditState state = ref gui.Storage.Get<ImTextEditState>(fieldId);

            string typed = _authorTyped ?? string.Empty;
            bool changed = gui.TextEdit(fieldId, ref typed, ref state, fieldRect, multiline: false, hint: "steam name…".AsSpan());
            if (gui.IsControlActive(fieldId))
                LevelBrowserInputGate.SuppressNavigatorInput = true;
            if (changed)
            {
                _authorTyped = typed;
                if (!string.IsNullOrEmpty(_session.AuthorUserId) &&
                    !string.Equals(typed, _session.AuthorUserName ?? string.Empty, StringComparison.Ordinal))
                {
                    _session.AuthorUserId = string.Empty;
                    _session.AuthorUserName = string.Empty;
                    OnFilterChanged(now);
                }

                _authorLookupAt = now + DebounceSeconds;
                if (string.IsNullOrWhiteSpace(typed) || typed.Trim().Length < 2)
                {
                    _authorSuggestions = Array.Empty<AuthorSuggestion>();
                    _authorLookupQuery = typed?.Trim() ?? string.Empty;
                    _authorListOpen = false;
                }
            }
        }
        finally
        {
            if (ownLevelsOnly)
                gui.EndReadOnly();
        }

        bool focused = gui.IsControlActive(fieldId);
        bool canShowList = !ownLevelsOnly &&
                           _authorSuggestions.Count > 0 &&
                           string.IsNullOrEmpty(_session.AuthorUserId);

        // Open while typing; keep open after TextEdit loses focus on the suggestion click Down.
        if (focused && canShowList)
            _authorListOpen = true;
        else if (!canShowList)
            _authorListOpen = false;

        if (_authorListOpen && canShowList)
            DrawAuthorSuggestionPopup(gui, fieldRect, fieldId);

        if (!string.IsNullOrEmpty(_session.AuthorUserId) && !ownLevelsOnly && gui.Button("Clear".AsSpan()))
            ClearAuthorUserSelection(scheduleLookup: false, now: now);
    }

    private void DrawAuthorSuggestionPopup(ImGui gui, ImRect fieldRect, uint fieldId)
    {
        // Same pattern as ImDropdown: menu popup with box background + Menu rows.
        // _authorListOpen stays true after TextEdit loses focus on the suggestion click Down,
        // so the menu is still drawn that frame and can receive the press.
        gui.BeginPopup();

        bool open = true;
        string scopeId = "uploaded-by-" + fieldId.ToString();
        if (gui.BeginMenuPopup(
                scopeId.AsSpan(),
                ref open,
                fieldRect.BottomLeft,
                fieldRect.W,
                ImMenuFlag.DoNotDismissOnClick))
        {
            for (int i = 0; i < _authorSuggestions.Count; i++)
            {
                AuthorSuggestion suggestion = _authorSuggestions[i];
                if (!gui.Menu(suggestion.SteamName.AsSpan()))
                    continue;

                _pendingAuthorPick = suggestion;
                _authorSuggestions = Array.Empty<AuthorSuggestion>();
                open = false;
                break;
            }

            gui.EndMenuPopup();
        }

        gui.EndPopup();

        if (!open)
            _authorListOpen = false;
    }

    private void ApplyAuthorPick(AuthorSuggestion pick, float now)
    {
        _authorTyped = pick.SteamName;
        _session.AuthorUserId = pick.SteamId;
        _session.AuthorUserName = pick.SteamName;
        _session.OwnLevelsOnly = false;
        _authorSuggestions = Array.Empty<AuthorSuggestion>();
        _authorLookupQuery = pick.SteamName;
        _authorLookupAt = -1f;
        _authorListOpen = false;
        OnFilterChanged(now);
    }

    private void ClearAuthorUserSelection(bool scheduleLookup, float now = 0f)
    {
        bool hadSelection = !string.IsNullOrEmpty(_session.AuthorUserId);
        _session.AuthorUserId = string.Empty;
        _session.AuthorUserName = string.Empty;
        _authorTyped = string.Empty;
        _authorSuggestions = Array.Empty<AuthorSuggestion>();
        _authorLookupQuery = string.Empty;
        _authorLookupAt = -1f;
        _authorListOpen = false;
        _pendingAuthorPick = null;
        _authorCts?.Cancel();

        if (hadSelection && now > 0f)
            OnFilterChanged(now);
        else if (scheduleLookup && now > 0f)
            _authorLookupAt = now + DebounceSeconds;
    }

    private static bool DrawFilterTextEdit(ImGui gui, ref string text, ReadOnlySpan<char> hint)
    {
        bool changed = gui.TextEdit(ref text, hint: hint);
        if (gui.IsControlActive(gui.LastControl))
            LevelBrowserInputGate.SuppressNavigatorInput = true;
        return changed;
    }

    private void DrawRailDropdown(
        ImGui gui,
        string label,
        string[] items,
        int selected,
        float now,
        Action<int> apply)
    {
        gui.Text(label.AsSpan(), gui.Style.Text.Color);
        int index = selected;
        if (index < 0 || index >= items.Length)
            index = 0;

        if (gui.Dropdown(ref index, items) && index != selected)
        {
            apply(index);
            OnFilterChanged(now);
        }
    }

    private void DrawRailCheckbox(
        ImGui gui,
        string label,
        bool value,
        float now,
        Action<bool> apply,
        bool enabled = true)
    {
        if (!enabled)
            gui.BeginReadOnly(true);

        try
        {
            bool next = value;
            if (gui.Checkbox(ref next, label.AsSpan()) && next != value)
            {
                apply(next);
                OnFilterChanged(now);
            }
        }
        finally
        {
            if (!enabled)
                gui.EndReadOnly();
        }
    }

    private void OnFilterChanged(float now)
    {
        _session.Page = 0;
        _fetchAt = now + DebounceSeconds;
    }

    private void DrawResults(ImGui gui, ImRect results, float now)
    {
        switch (_state)
        {
            case ResultsState.Loading:
                DrawCentered(gui, results, "Loading…", gui.Style.TextEdit.HintFrontColor);
                return;
            case ResultsState.Error:
                DrawError(gui, results, now);
                return;
            default:
                if (_rows.Count == 0)
                    DrawCentered(gui, results, "No levels match", gui.Style.TextEdit.HintFrontColor);
                else
                    DrawCards(gui, results, now);
                return;
        }
    }

    private void DrawCards(ImGui gui, ImRect results, float now)
    {
        gui.Layout.Push(ImAxis.Vertical, results);
        gui.Canvas.PushClipRect(results);
        gui.BeginScrollable();

        float width = gui.GetLayoutWidth();
        for (int i = 0; i < _rows.Count; i++)
        {
            ImRect card = gui.AddLayoutRect(width, CardHeight);
            DrawCard(gui, card, _rows[i], now);
            if (i < _rows.Count - 1)
                gui.AddSpacing(CardSpacing);
        }

        gui.EndScrollable();
        gui.Canvas.PopClipRect();
        gui.Layout.Pop();
    }

    private void DrawCard(ImGui gui, ImRect card, LevelBrowseRow row, float now)
    {
        gui.Canvas.Rect(card, gui.Style.List.ItemNormal.Normal.BackColor);

        const float pad = 6f;
        var thumb = new ImRect(card.X + pad, card.Y + pad, ThumbWidth, card.H - pad * 2f);
        DrawThumbnail(gui, thumb, row);

        float textX = thumb.Right + Gap;
        float textRight = card.Right - pad - ActionsWidth - Gap;
        var textRect = new ImRect(textX, card.Y + pad, Mathf.Max(0f, textRight - textX), card.H - pad * 2f);

        var nameRect = new ImRect(textRect.X, textRect.Y + textRect.H * 0.5f, textRect.W, textRect.H * 0.5f);
        var authorRect = new ImRect(textRect.X, textRect.Y, textRect.W, textRect.H * 0.5f);
        var nameSettings = new ImTextSettings(gui.Style.Layout.TextSize, 0f, 0.5f);
        var authorSettings = new ImTextSettings(gui.Style.Layout.TextSize * 0.85f, 0f, 0.5f);
        gui.Canvas.Text(SafeText(row.Name).AsSpan(), gui.Style.Text.Color, nameRect, in nameSettings);
        gui.Canvas.Text(SafeText(row.FileAuthor).AsSpan(), gui.Style.TextEdit.HintFrontColor, authorRect, in authorSettings);

        bool alreadyIn = _session.IsAlreadyIn(row.FileUid);
        var actionRect = new ImRect(
            card.Right - pad - ActionsWidth,
            card.Y + (card.H - ButtonHeight) * 0.5f,
            ActionsWidth,
            ButtonHeight);

        if (alreadyIn)
        {
            DrawBadge(gui, actionRect, "In playlist");
            if (gui.InvisibleButton(card))
                TrySelect(row, now);
        }
        else
        {
            uint id = gui.GetNextControlId();
            if (gui.Button(id, "Select".AsSpan(), actionRect, out _))
                TrySelect(row, now);
        }
    }

    private void DrawThumbnail(ImGui gui, ImRect thumb, LevelBrowseRow row)
    {
        Texture2D texture = row.HasThumbnail ? _thumbnails.Get(row.ImageUrl) : null;
        if (texture != null)
        {
            gui.Image(texture, thumb, true);
            return;
        }

        gui.Canvas.Rect(thumb, gui.Style.TextEdit.Normal.Box.BackColor);
        var settings = new ImTextSettings(gui.Style.Layout.TextSize * 0.7f, 0.5f, 0.5f);
        gui.Canvas.Text("no image".AsSpan(), gui.Style.TextEdit.HintFrontColor, thumb, in settings);
    }

    private void DrawBadge(ImGui gui, ImRect rect, string label)
    {
        gui.Canvas.Rect(rect, gui.Style.AccentButton.Normal.BackColor);
        var settings = new ImTextSettings(gui.Style.Layout.TextSize * 0.8f, 0.5f, 0.5f);
        gui.Canvas.Text(label.AsSpan(), gui.Style.AccentButton.Normal.FrontColor, rect, in settings);
    }

    private void TrySelect(LevelBrowseRow row, float now)
    {
        SelectionOutcome outcome = _session.TrySelect(row.ToSelection());
        if (outcome == SelectionOutcome.AlreadyIn)
            ShowToast("Already in the playlist", now);
    }

    private void DrawError(ImGui gui, ImRect results, float now)
    {
        float rowHeight = gui.GetRowHeight();
        var messageRect = new ImRect(
            results.X,
            results.Y + results.H * 0.5f + Gap,
            results.W,
            rowHeight);
        var settings = new ImTextSettings(gui.Style.Layout.TextSize, 0.5f, 0.5f);
        gui.Canvas.Text("Couldn’t load levels".AsSpan(), gui.Style.Text.Color, messageRect, in settings);

        const float retryWidth = 100f;
        var retryRect = new ImRect(
            results.X + (results.W - retryWidth) * 0.5f,
            results.Y + results.H * 0.5f - ButtonHeight - Gap,
            retryWidth,
            ButtonHeight);
        uint id = gui.GetNextControlId();
        if (gui.Button(id, "Retry".AsSpan(), retryRect, out _))
            _fetchAt = now;
    }

    private void DrawPager(ImGui gui, ImRect pager, float now)
    {
        const float navButtonWidth = 64f;
        const float numberMinWidth = 32f;
        const float ellipsisWidth = 18f;
        const float numberGap = 4f;
        const float sideGap = 8f;
        // Match ImButton.CalculateContentRect padding, plus a little breathing room.
        float numberPadX = gui.Style.Layout.InnerSpacing * 2f + 8f;

        int page = _session.Page < 0 ? 0 : _session.Page;
        int totalPages = Mathf.Max(1, Mathf.CeilToInt(_totalCount / (float)PageSize));
        if (page >= totalPages)
            page = totalPages - 1;

        bool canPrev = page > 0;
        bool canNext = _appliedOffset + _rows.Count < _totalCount;

        var prevRect = new ImRect(pager.X, pager.Y, navButtonWidth, pager.H);
        if (DrawPagerButton(gui, prevRect, "Prev", canPrev, accent: false) && canPrev)
        {
            _session.Page = page - 1;
            _fetchAt = now;
        }

        var nextRect = new ImRect(pager.Right - navButtonWidth, pager.Y, navButtonWidth, pager.H);
        if (DrawPagerButton(gui, nextRect, "Next", canNext, accent: false) && canNext)
        {
            _session.Page = page + 1;
            _fetchAt = now;
        }

        float middleLeft = prevRect.Right + sideGap;
        float middleRight = nextRect.X - sideGap;
        float middleW = Mathf.Max(0f, middleRight - middleLeft);

        IReadOnlyList<int> slots = LevelBrowserPageWindow.Build(page, totalPages);
        float textSize = gui.Style.Layout.TextSize;
        float runWidth = 0f;
        for (int i = 0; i < slots.Count; i++)
        {
            if (i > 0)
                runWidth += numberGap;
            if (slots[i] == LevelBrowserPageWindow.Ellipsis)
            {
                runWidth += ellipsisWidth;
                continue;
            }

            string label = (slots[i] + 1).ToString();
            float labelW = gui.MeasureTextSize(label.AsSpan(), textSize).x;
            runWidth += Mathf.Max(numberMinWidth, labelW + numberPadX);
        }

        float x = middleLeft + Mathf.Max(0f, (middleW - runWidth) * 0.5f);
        var ellipsisSettings = new ImTextSettings(textSize, 0.5f, 0.5f);
        for (int i = 0; i < slots.Count; i++)
        {
            int slot = slots[i];
            if (slot == LevelBrowserPageWindow.Ellipsis)
            {
                var ellipsisRect = new ImRect(x, pager.Y, ellipsisWidth, pager.H);
                gui.Canvas.Text("…".AsSpan(), gui.Style.TextEdit.HintFrontColor, ellipsisRect, in ellipsisSettings);
                x += ellipsisWidth + numberGap;
                continue;
            }

            string label = (slot + 1).ToString();
            float labelW = gui.MeasureTextSize(label.AsSpan(), textSize).x;
            float numberWidth = Mathf.Max(numberMinWidth, labelW + numberPadX);
            var numberRect = new ImRect(x, pager.Y, numberWidth, pager.H);
            bool isCurrent = slot == page;
            if (DrawPagerButton(gui, numberRect, label, enabled: !isCurrent, accent: isCurrent) && !isCurrent)
            {
                _session.Page = slot;
                _fetchAt = now;
            }

            x += numberWidth + numberGap;
        }
    }

    private bool DrawPagerButton(ImGui gui, ImRect rect, string label, bool enabled, bool accent)
    {
        if (!enabled)
            gui.BeginReadOnly(true);

        try
        {
            uint id = gui.GetNextControlId();
            if (accent)
                return gui.Button(id, label.AsSpan(), rect, in gui.Style.AccentButton, out _) && enabled;

            return gui.Button(id, label.AsSpan(), rect, out _) && enabled;
        }
        finally
        {
            if (!enabled)
                gui.EndReadOnly();
        }
    }

    private void ShowToast(string message, float now)
    {
        _toast = message;
        _toastUntil = now + ToastSeconds;
    }

    private void DrawToast(ImGui gui, ImRect area, float now)
    {
        if (_toast == null || now >= _toastUntil)
            return;

        const float toastWidth = 220f;
        float toastHeight = gui.GetRowHeight();
        var rect = new ImRect(
            area.X + (area.W - toastWidth) * 0.5f,
            area.Y + Gap,
            toastWidth,
            toastHeight);
        gui.Canvas.Rect(rect, gui.Style.AccentButton.Normal.BackColor);
        var settings = new ImTextSettings(gui.Style.Layout.TextSize * 0.85f, 0.5f, 0.5f);
        gui.Canvas.Text(_toast.AsSpan(), gui.Style.AccentButton.Normal.FrontColor, rect, in settings);
    }

    private static string SafeText(string value)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : value;
    }

    private void DrawCentered(ImGui gui, ImRect rect, string message, Color32 color)
    {
        var settings = new ImTextSettings(gui.Style.Layout.TextSize, 0.5f, 0.5f);
        gui.Canvas.Text(message.AsSpan(), color, rect, in settings);
    }
}

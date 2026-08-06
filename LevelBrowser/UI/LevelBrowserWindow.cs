using System;
using System.Collections.Generic;
using System.Threading;
using Imui.Controls;
using Imui.Core;
using Imui.Rendering;
using TNRD.Zeepkist.GTR.UI;
using UnityEngine;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;
using ZeepSDK.UI;

namespace TNRD.Zeepkist.GTR.LevelBrowser.UI;

/// <summary>
/// The Level Browser Imui window (layout B): a left name/author filter rail and a right results
/// area with thumbnail cards, pagination below, and shared loading / empty / error+Retry states.
/// Driven entirely by the <see cref="LevelBrowserSession"/> (open state, filters, page, already-in
/// marking, Selection delivery) and the <see cref="LevelBrowseService"/> (catalog fetch).
/// </summary>
public sealed class LevelBrowserWindow : IZeepGUIDrawer
{
    private const string WindowTitle = "Level Browser";
    private const float DefaultWidth = 960f;
    private const float DefaultHeight = 720f;
    private const float RailWidth = 172f;
    private const float Gap = 8f;
    private const float CardHeight = 62f;
    private const float CardSpacing = 6f;
    private const float ThumbWidth = 96f;
    private const float ActionsWidth = 112f;
    private const float ButtonHeight = 28f;
    private const int PageSize = LevelItemsBrowseQueryBuilder.DefaultPageSize;
    private const float DebounceSeconds = 0.3f;
    private const float ToastSeconds = 2.5f;

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
    private int _appliedPage = -1;

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
            _wasOpen = false;
            return;
        }

        float now = Time.unscaledTime;

        if (!_wasOpen)
            OnOpened(now);
        _wasOpen = true;

        _thumbnails.Poll();
        MaybeRefetch(now);

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
        _appliedPage = -1;
        _toast = null;
        _fetchAt = now;
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
        if (_fetchAt < 0f && _state != ResultsState.Loading)
        {
            if (!string.Equals(_session.SearchName ?? string.Empty, _appliedName, StringComparison.Ordinal) ||
                !string.Equals(_session.SearchAuthor ?? string.Empty, _appliedAuthor, StringComparison.Ordinal) ||
                _session.Page != _appliedPage)
            {
                _fetchAt = now;
            }
        }
    }

    private void BeginFetch()
    {
        string name = _session.SearchName ?? string.Empty;
        string author = _session.SearchAuthor ?? string.Empty;
        int page = _session.Page < 0 ? 0 : _session.Page;

        _appliedName = name;
        _appliedAuthor = author;
        _appliedPage = page;
        _appliedOffset = page * PageSize;

        _state = ResultsState.Loading;
        _rows = Array.Empty<LevelBrowseRow>();

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        _requestId++;
        FetchAsync(_requestId, name, author, page, _cts.Token).Forget();
    }

    private async UniTaskVoid FetchAsync(int requestId, string name, string author, int page, CancellationToken ct)
    {
        Result<LevelBrowsePage> result = await _service.BrowseAsync(name, author, page, ct);

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
        float labelHeight = gui.GetRowHeight() * 0.8f;
        float fieldHeight = gui.GetRowHeight();
        float cursor = rail.Top;

        cursor = DrawRailLabel(gui, rail, cursor, "Name", labelHeight);
        string name = _session.SearchName ?? string.Empty;
        var nameRect = new ImRect(rail.X, cursor - fieldHeight, rail.W, fieldHeight);
        if (gui.TextEdit(ref name, nameRect, false, hint: "includes…".AsSpan()))
        {
            _session.SearchName = name;
            OnFilterChanged(now);
        }
        cursor -= fieldHeight + Gap;

        cursor = DrawRailLabel(gui, rail, cursor, "Author", labelHeight);
        string author = _session.SearchAuthor ?? string.Empty;
        var authorRect = new ImRect(rail.X, cursor - fieldHeight, rail.W, fieldHeight);
        if (gui.TextEdit(ref author, authorRect, false, hint: "fileAuthor…".AsSpan()))
        {
            _session.SearchAuthor = author;
            OnFilterChanged(now);
        }
    }

    private float DrawRailLabel(ImGui gui, ImRect rail, float cursor, string label, float height)
    {
        var rect = new ImRect(rail.X, cursor - height, rail.W, height);
        var settings = new ImTextSettings(gui.Style.Layout.TextSize * 0.85f, 0f, 0.5f);
        gui.Canvas.Text(label.AsSpan(), gui.Style.TextEdit.HintFrontColor, rect, in settings);
        return cursor - height;
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
        const float buttonWidth = 76f;
        int page = _session.Page < 0 ? 0 : _session.Page;
        bool canPrev = page > 0;
        bool canNext = _appliedOffset + _rows.Count < _totalCount;

        var prevRect = new ImRect(pager.X, pager.Y, buttonWidth, pager.H);
        if (DrawPagerButton(gui, prevRect, "Prev", canPrev) && canPrev)
        {
            _session.Page = page - 1;
            _fetchAt = now;
        }

        var nextRect = new ImRect(pager.Right - buttonWidth, pager.Y, buttonWidth, pager.H);
        if (DrawPagerButton(gui, nextRect, "Next", canNext) && canNext)
        {
            _session.Page = page + 1;
            _fetchAt = now;
        }

        var infoRect = new ImRect(prevRect.Right, pager.Y, nextRect.X - prevRect.Right, pager.H);
        string info = _state == ResultsState.Loaded
            ? $"Page {page + 1} · {_totalCount} levels"
            : $"Page {page + 1}";
        var settings = new ImTextSettings(gui.Style.Layout.TextSize * 0.85f, 0.5f, 0.5f);
        gui.Canvas.Text(info.AsSpan(), gui.Style.TextEdit.HintFrontColor, infoRect, in settings);
    }

    private bool DrawPagerButton(ImGui gui, ImRect rect, string label, bool enabled)
    {
        if (!enabled)
            gui.BeginReadOnly(true);

        try
        {
            uint id = gui.GetNextControlId();
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

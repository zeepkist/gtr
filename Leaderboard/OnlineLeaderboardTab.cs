using System;
using System.Collections.Generic;
using System.Threading;
using TNRD.Zeepkist.GTR.GraphQL;
using TNRD.Zeepkist.GTR.Messaging;
using UnityEngine;
using ZeepkistClient;
using ZeepSDK.Extensions;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;
using ZeepSDK.Leaderboard.Pages;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public class OnlineLeaderboardTab : BaseMultiplayerLeaderboardTab, IDisposable
{
    private readonly LeaderboardGraphqlService _graphqlService;
    private readonly MessengerService _messengerService;
    private readonly List<LeaderboardRecord> _items = [];

    private CancellationTokenSource _cancellationTokenSource;
    private IDisposable _subscription;
    private int _generation;
    private int? _levelPoints;
    private string _title = "GTR Records";

    public OnlineLeaderboardTab(LeaderboardGraphqlService graphqlService, MessengerService messengerService)
    {
        _graphqlService = graphqlService;
        _messengerService = messengerService;
        RacingApi.LevelLoaded += StopForContextChange;
        RacingApi.Quit += StopForContextChange;
        MultiplayerApi.DisconnectedFromGame += StopForContextChange;
    }

    protected override string GetLeaderboardTitle()
    {
        return _title;
    }

    protected override void OnEnable()
    {
        _title = "GTR Records";
        LoadPage(CurrentPage);
    }

    protected override void OnDisable()
    {
        StopPage();
    }

    protected override void OnDraw()
    {
        for (int i = 0; i < Instance.leaderboard_tab_positions.Count; i++)
        {
            GUI_OnlineLeaderboardPosition gui = Instance.leaderboard_tab_positions[i];
            if (i >= _items.Count)
                continue;

            gui.gameObject.SetActive(true);
            int index = CurrentPage * Instance.leaderboard_tab_positions.Count + i;
            OnDrawItem(gui, _items[i], index);
        }
    }

    protected override void OnPageChanged(int previous, int current)
    {
        LoadPage(current);
    }

    private void LoadPage(int page)
    {
        StopPage();
        LevelGraphqlIdentity level = CurrentLevelGraphqlIdentity.Create();
        if (!level.IsAvailable)
            return;

        int generation = ++_generation;
        int pageSize = Instance.leaderboard_tab_positions.Count;
        _cancellationTokenSource = new CancellationTokenSource();
        _subscription = _graphqlService.WatchPage(
            level,
            page,
            pageSize,
            snapshot => ApplySnapshotAsync(snapshot, generation).Forget(),
            error => Logger.LogWarning("GTR leaderboard subscription failed: " + error));
        LoadInitialAsync(level, page, pageSize, generation, _cancellationTokenSource.Token).Forget();
    }

    private async UniTaskVoid LoadInitialAsync(
        LevelGraphqlIdentity level,
        int page,
        int pageSize,
        int generation,
        CancellationToken cancellationToken)
    {
        Result<LeaderboardPageSnapshot> result =
            await _graphqlService.GetPage(level, page, pageSize, cancellationToken);
        if (cancellationToken.IsCancellationRequested || generation != _generation)
            return;
        if (result.IsFailed)
        {
            Logger.LogError("Failed to load GTR records: " + result);
            _messengerService.LogError("Failed to load GTR records");
            return;
        }

        ApplySnapshot(result.Value);
    }

    private async UniTaskVoid ApplySnapshotAsync(LeaderboardPageSnapshot snapshot, int generation)
    {
        await UniTask.SwitchToMainThread();
        if (generation != _generation)
            return;
        ApplySnapshot(snapshot);
    }

    private void ApplySnapshot(LeaderboardPageSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        _levelPoints = snapshot.LevelPoints;
        _items.Clear();
        _items.AddRange(snapshot.Records);
        MaxPages = LeaderboardPagination.GetMaxPageIndex(
            snapshot.TotalRecords,
            Instance.leaderboard_tab_positions.Count);
        UpdatePageNumber();
        _title = LeaderboardTextFormatter.FormatTitle(snapshot.LevelName);
        Instance.leaderboardTitle.text = _title;
        Draw();
    }

    private void OnDrawItem(GUI_OnlineLeaderboardPosition gui, LeaderboardRecord item, int index)
    {
        ZeepkistNetwork.TryGetPlayer(Convert.ToUInt64(item.SteamId), out gui.thePlayer);

        gui.position.gameObject.SetActive(true);
        gui.position.text = (index + 1).ToString();
        gui.position.color = PlayerManager.Instance.GetColorFromPosition(index + 1);
        gui.favoriteButton.gameObject.SetActive(false);

        string playerMarkup;
        if (ZeepkistNetwork.LocalPlayer.SteamID.ToString() == item.SteamId)
        {
            string playerColor = ColorUtility.ToHtmlStringRGB(ZeepkistNetwork.LocalPlayer.chatColor);
            playerMarkup = $"<color=#{playerColor}><link=\"{item.SteamId}\">{item.SteamName}</link></color>";
        }
        else if (gui.thePlayer != null && gui.thePlayer.SteamID.ToString() == item.SteamId)
        {
            string playerColor = ColorUtility.ToHtmlStringRGB(gui.thePlayer.chatColor);
            playerMarkup = $"<color=#{playerColor}><link=\"{item.SteamId}\">{item.SteamName}</link></color>";
        }
        else
        {
            playerMarkup = $"<link=\"{item.SteamId}\">{item.SteamName}</link>";
        }

        gui.player_name.text = LeaderboardTextFormatter.AppendRecordDate(
            playerMarkup,
            item.DateCreated,
            DateTimeOffset.Now);
        gui.time.text = item.Time.GetFormattedTime();
        gui.pointsWon.gameObject.SetActive(_levelPoints.HasValue);
        if (_levelPoints.HasValue)
            gui.pointsWon.text = $"(+{(int)Math.Round(_levelPoints.Value * Math.Pow(0.985, index))})";
    }

    private void StopPage()
    {
        _generation++;
        _subscription?.Dispose();
        _subscription = null;
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }

    private void StopForContextChange()
    {
        StopPage();
        _items.Clear();
        _title = "GTR Records";
    }

    public void Dispose()
    {
        RacingApi.LevelLoaded -= StopForContextChange;
        RacingApi.Quit -= StopForContextChange;
        MultiplayerApi.DisconnectedFromGame -= StopForContextChange;
        StopPage();
    }
}

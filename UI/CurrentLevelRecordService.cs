using System;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Steamworks;
using StrawberryShake;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.GraphQL;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.Level;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.UI;

public sealed class CurrentLevelRecordService : IEagerService, IDisposable
{
    private readonly IGtrClient _gtrClient;
    private readonly ILogger<CurrentLevelRecordService> _logger;

    private CancellationTokenSource _cancellationTokenSource;
    private IDisposable _subscription;
    private int _generation;
    private LevelGraphqlIdentity _level;

    public CurrentLevelRecordService(IGtrClient gtrClient, ILogger<CurrentLevelRecordService> logger)
    {
        _gtrClient = gtrClient;
        _logger = logger;

        RacingApi.LevelLoaded += Restart;
        RacingApi.PlayerSpawned += Restart;
        RacingApi.Quit += Stop;
        MultiplayerApi.DisconnectedFromGame += Stop;
    }

    public CurrentLevelRecordSnapshot Snapshot { get; private set; }

    public event Action<CurrentLevelRecordSnapshot> SnapshotChanged;

    public void Refresh()
    {
        int generation = _generation;
        if (!_level.IsAvailable)
            return;
        LoadInitialAsync(_level, generation, _cancellationTokenSource?.Token ?? default).Forget();
    }

    private void Restart()
    {
        StopCore();
        _level = CurrentLevelGraphqlIdentity.Create();
        if (!_level.IsAvailable)
        {
            _logger.LogWarning("Unable to start current-level record stream without level identity");
            return;
        }

        int generation = ++_generation;
        _cancellationTokenSource = new CancellationTokenSource();
        LoadInitialAsync(_level, generation, _cancellationTokenSource.Token).Forget();

        _subscription = _gtrClient.WatchCurrentLevelRecords
            .Watch(_level.XxHash, _level.Hash, SteamClient.SteamId.ToString())
            .Subscribe(new OperationObserver<IOperationResult<IWatchCurrentLevelRecordsResult>>(
                result => OnSubscriptionResult(result, _level, generation).Forget(),
                error => _logger.LogWarning(error, "Current-level record subscription failed")));
    }

    private async UniTaskVoid LoadInitialAsync(
        LevelGraphqlIdentity level,
        int generation,
        CancellationToken cancellationToken)
    {
        try
        {
            IOperationResult<ICurrentLevelRecordsResult> result = await _gtrClient.CurrentLevelRecords.ExecuteAsync(
                level.XxHash,
                level.Hash,
                SteamClient.SteamId.ToString(),
                cancellationToken);
            result.EnsureNoErrors();
            CurrentLevelRecordSnapshot snapshot = Map(result.Data, level.CacheKey);
            await EnrichRank(snapshot, level, cancellationToken);
            await Publish(snapshot, generation, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to load current-level record snapshot");
        }
    }

    private async UniTaskVoid OnSubscriptionResult(
        IOperationResult<IWatchCurrentLevelRecordsResult> result,
        LevelGraphqlIdentity level,
        int generation)
    {
        try
        {
            result.EnsureNoErrors();
            CurrentLevelRecordSnapshot snapshot = Map(result.Data?.Query, level.CacheKey);
            CancellationToken cancellationToken = _cancellationTokenSource?.Token ?? default;
            await EnrichRank(snapshot, level, cancellationToken);
            await Publish(snapshot, generation, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to process current-level record subscription snapshot");
        }
    }

    private async UniTask EnrichRank(
        CurrentLevelRecordSnapshot snapshot,
        LevelGraphqlIdentity level,
        CancellationToken cancellationToken)
    {
        if (snapshot?.PersonalBest == null || snapshot.PersonalBest.Rank.HasValue)
            return;

        IOperationResult<IGetPlayerRankOnLevelResult> result = await _gtrClient.GetPlayerRankOnLevel.ExecuteAsync(
            level.XxHash,
            level.Hash,
            snapshot.PersonalBest.Time,
            cancellationToken);
        result.EnsureNoErrors();
        snapshot.PersonalBest.Rank = (result.Data?.Records?.TotalCount ?? 0) + 1;
    }

    private async UniTask Publish(
        CurrentLevelRecordSnapshot snapshot,
        int generation,
        CancellationToken cancellationToken)
    {
        await UniTask.SwitchToMainThread(cancellationToken);
        if (generation != _generation || cancellationToken.IsCancellationRequested)
            return;

        Snapshot = snapshot;
        SnapshotChanged?.Invoke(snapshot);
    }

    private static CurrentLevelRecordSnapshot Map(ICurrentLevelRecordsResult data, string levelKey)
    {
        ICurrentLevelRecords_PersonalBestGlobals_Nodes_Record personalBest =
            data?.PersonalBestGlobals?.Nodes.FirstOrDefault()?.Record;
        ICurrentLevelRecords_PersonalBestGlobals_Nodes_Record_UserPointContributions_Nodes contribution =
            personalBest?.UserPointContributions?.Nodes.FirstOrDefault();
        ICurrentLevelRecords_WorldRecordGlobals_Nodes_Record worldRecord =
            data?.WorldRecordGlobals?.Nodes.FirstOrDefault()?.Record;

        return CreateSnapshot(
            levelKey,
            data?.LevelItems?.Nodes.FirstOrDefault()?.Name,
            data?.LevelPoints?.Nodes.FirstOrDefault()?.Points,
            personalBest?.Id,
            personalBest?.Time,
            personalBest?.DateCreated,
            contribution?.ContributionRank,
            contribution?.LevelPosition,
            contribution?.LevelPoints,
            contribution?.LevelDecayedPoints,
            contribution?.PlayerDecayedPoints,
            worldRecord?.Id,
            worldRecord?.Time,
            worldRecord?.User?.SteamId,
            worldRecord?.User?.SteamName);
    }

    private static CurrentLevelRecordSnapshot Map(IWatchCurrentLevelRecords_Query data, string levelKey)
    {
        IWatchCurrentLevelRecords_Query_PersonalBestGlobals_Nodes_Record personalBest =
            data?.PersonalBestGlobals?.Nodes.FirstOrDefault()?.Record;
        IWatchCurrentLevelRecords_Query_PersonalBestGlobals_Nodes_Record_UserPointContributions_Nodes contribution =
            personalBest?.UserPointContributions?.Nodes.FirstOrDefault();
        IWatchCurrentLevelRecords_Query_WorldRecordGlobals_Nodes_Record worldRecord =
            data?.WorldRecordGlobals?.Nodes.FirstOrDefault()?.Record;

        return CreateSnapshot(
            levelKey,
            data?.LevelItems?.Nodes.FirstOrDefault()?.Name,
            data?.LevelPoints?.Nodes.FirstOrDefault()?.Points,
            personalBest?.Id,
            personalBest?.Time,
            personalBest?.DateCreated,
            contribution?.ContributionRank,
            contribution?.LevelPosition,
            contribution?.LevelPoints,
            contribution?.LevelDecayedPoints,
            contribution?.PlayerDecayedPoints,
            worldRecord?.Id,
            worldRecord?.Time,
            worldRecord?.User?.SteamId,
            worldRecord?.User?.SteamName);
    }

    private static CurrentLevelRecordSnapshot CreateSnapshot(
        string levelKey,
        string levelName,
        int? levelPoints,
        int? personalBestId,
        double? personalBestTime,
        string personalBestDate,
        int? contributionRank,
        int? levelPosition,
        int? contributionLevelPoints,
        double? levelDecayedPoints,
        double? playerDecayedPoints,
        int? worldRecordId,
        double? worldRecordTime,
        string worldRecordSteamId,
        string worldRecordSteamName)
    {
        return new CurrentLevelRecordSnapshot
        {
            LevelKey = levelKey,
            LevelName = levelName,
            LevelPoints = levelPoints,
            PersonalBest = personalBestId.HasValue && personalBestTime.HasValue
                ? new PersonalBestHolder
                {
                    RecordId = personalBestId.Value,
                    Time = personalBestTime.Value,
                    DateCreated = personalBestDate,
                    ContributionRank = contributionRank,
                    Rank = levelPosition,
                    LevelPoints = contributionLevelPoints,
                    LevelDecayedPoints = levelDecayedPoints,
                    PlayerDecayedPoints = playerDecayedPoints
                }
                : null,
            WorldRecord = worldRecordId.HasValue && worldRecordTime.HasValue
                ? new WorldRecordHolder
                {
                    RecordId = worldRecordId.Value,
                    Time = worldRecordTime.Value,
                    SteamId = worldRecordSteamId,
                    SteamName = worldRecordSteamName
                }
                : null
        };
    }

    private void Stop()
    {
        StopCore();
        _level = LevelGraphqlIdentity.Unavailable;
        Snapshot = null;
    }

    private void StopCore()
    {
        _generation++;
        _subscription?.Dispose();
        _subscription = null;
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }

    public void Dispose()
    {
        RacingApi.LevelLoaded -= Restart;
        RacingApi.PlayerSpawned -= Restart;
        RacingApi.Quit -= Stop;
        MultiplayerApi.DisconnectedFromGame -= Stop;
        Stop();
    }
}

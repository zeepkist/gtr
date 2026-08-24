using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Steamworks;
using StrawberryShake;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.GraphQL;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.UI;

public sealed class CurrentLevelRecordService : IEagerService, IDisposable
{
    private readonly IGtrClient _gtrClient;
    private readonly ILogger<CurrentLevelRecordService> _logger;

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

    private void Restart()
    {
        StopCore();
        Snapshot = null;
        _level = CurrentLevelGraphqlIdentity.Create();
        if (!_level.IsAvailable)
        {
            _logger.LogWarning("Unable to start current-level record stream without level identity");
            return;
        }

        int generation = ++_generation;
        _subscription = _gtrClient.WatchCurrentLevelRecords
            .Watch(_level.XxHash, SteamClient.SteamId.ToString())
            .Subscribe(new OperationObserver<IOperationResult<IWatchCurrentLevelRecordsResult>>(
                result => OnSubscriptionResult(result, _level.CacheKey, generation).Forget(),
                error => _logger.LogWarning(error, "Current-level record subscription failed")));
    }

    private async UniTaskVoid OnSubscriptionResult(
        IOperationResult<IWatchCurrentLevelRecordsResult> result,
        string levelKey,
        int generation)
    {
        try
        {
            result.EnsureNoErrors();
            CurrentLevelRecordSnapshot snapshot = Map(result.Data?.Query, levelKey);
            await UniTask.SwitchToMainThread();
            if (generation != _generation)
                return;

            Snapshot = snapshot;
            SnapshotChanged?.Invoke(snapshot);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to process current-level record subscription snapshot");
        }
    }

    private static CurrentLevelRecordSnapshot Map(IWatchCurrentLevelRecords_Query data, string levelKey)
    {
        IWatchCurrentLevelRecords_Query_PersonalBest_Nodes personalBest =
            data?.PersonalBest?.Nodes.FirstOrDefault();
        IWatchCurrentLevelRecords_Query_WorldRecord_Nodes worldRecord =
            data?.WorldRecord?.Nodes.FirstOrDefault();

        return new CurrentLevelRecordSnapshot
        {
            LevelKey = levelKey,
            PersonalBest = personalBest?.Time.HasValue != true
                ? null
                : new PersonalBestHolder
                {
                    Time = personalBest.Time.Value,
                    Rank = personalBest.LevelPosition,
                    LevelDecayedPoints = personalBest.LevelDecayedPoints,
                    PlayerDecayedPoints = personalBest.PlayerDecayedPoints
                },
            WorldRecord = worldRecord?.Time.HasValue != true
                ? null
                : new WorldRecordHolder
                {
                    Time = worldRecord.Time.Value,
                    Rank = worldRecord.LevelPosition,
                    SteamId = worldRecord.UserSteamId,
                    SteamName = worldRecord.UserName,
                    LevelDecayedPoints = worldRecord.LevelDecayedPoints,
                    PlayerDecayedPoints = worldRecord.PlayerDecayedPoints
                }
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

using System;
using System.Collections.Generic;
using System.Linq;
using StrawberryShake;
using TNRD.Zeepkist.GTR.GraphQL;
using ZeepSDK.External.Cysharp.Threading.Tasks;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public class LeaderboardGraphqlService
{
    private readonly IGtrClient _gtrClient;

    public LeaderboardGraphqlService(IGtrClient gtrClient)
    {
        _gtrClient = gtrClient;
    }

    public IDisposable WatchPage(
        LevelGraphqlIdentity level,
        int page,
        int pageSize,
        Action<LeaderboardPageSnapshot> onNext,
        Action<Exception> onError)
    {
        var medalTrackers = new Dictionary<string, UI.Medals.MedalUpgradeTracker>();
        int medalAttempt = 0;
        RecoveringSubscription<IOperationResult<IWatchLeaderboardPageResult>> subscription = null;
        subscription = new RecoveringSubscription<IOperationResult<IWatchLeaderboardPageResult>>(
            observer => _gtrClient.WatchLeaderboardPage.Watch(level.XxHash, level.Hash, pageSize, page * pageSize).Subscribe(observer),
            (result, attempt) => Deliver(result, attempt).Forget(), onError);
        subscription.Start();
        return subscription;

        async UniTaskVoid Deliver(IOperationResult<IWatchLeaderboardPageResult> result, int attempt)
        {
            try
            {
                result.EnsureNoErrors();
                if (result.Data?.Query == null) return;
                var snapshot = Map(result.Data?.Query);
                await UniTask.SwitchToMainThread();
                if (!subscription.IsCurrentAttempt(attempt)) return;
                if (medalAttempt != attempt) medalTrackers.Clear();
                medalAttempt = attempt;
                var visibleUsers = new HashSet<string>();
                foreach (LeaderboardRecord record in snapshot.Records)
                {
                    if (string.IsNullOrEmpty(record.SteamId)) continue;
                    visibleUsers.Add(record.SteamId);
                    if (!medalTrackers.TryGetValue(record.SteamId, out var tracker))
                        medalTrackers[record.SteamId] = tracker = new UI.Medals.MedalUpgradeTracker();
                    record.MedalUpgrade = tracker.Update(level.CacheKey, record.Time, record.Medal);
                }
                foreach (string steamId in medalTrackers.Keys.ToArray())
                    if (!visibleUsers.Contains(steamId)) medalTrackers.Remove(steamId);
                subscription.MarkHealthy(attempt);
                onNext(snapshot);
            }
            catch (Exception error)
            {
                if (subscription.IsCurrentAttempt(attempt)) onError(error);
            }
        }
    }

    private static LeaderboardPageSnapshot Map(IWatchLeaderboardPage_Query data)
    {
        IReadOnlyList<LeaderboardRecord> records = data?.Records?.Nodes
            .Select(record =>
            {
                IWatchLeaderboardPage_Query_Records_Nodes_UserPointContributions_Nodes contribution =
                    record.UserPointContributions?.Nodes.FirstOrDefault();
                return new LeaderboardRecord
                {
                    SteamId = record.User?.SteamId,
                    SteamName = record.User?.SteamName,
                    Time = record.Time,
                    Medal = record.Medal,
                    DateCreated = record.DateCreated,
                    LevelPosition = contribution?.LevelPosition,
                    LevelDecayedPoints = contribution?.LevelDecayedPoints
                };
            })
            .ToList() ?? new List<LeaderboardRecord>();

        return new LeaderboardPageSnapshot
        {
            LevelName = data?.Records?.Nodes.FirstOrDefault()?.Level?.LevelItems?.Nodes.FirstOrDefault()?.Name,
            TotalRecords = data?.Records?.TotalCount ?? 0,
            Records = records
        };
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using StrawberryShake;
using TNRD.Zeepkist.GTR.GraphQL;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public class LeaderboardGraphqlService
{
    private readonly IGtrClient _gtrClient;

    public LeaderboardGraphqlService(IGtrClient gtrClient)
    {
        _gtrClient = gtrClient;
    }

    public async UniTask<Result<LeaderboardPageSnapshot>> GetPage(
        LevelGraphqlIdentity level,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            IOperationResult<ILeaderboardPageResult> result = await _gtrClient.LeaderboardPage.ExecuteAsync(
                level.XxHash,
                level.Hash,
                pageSize,
                page * pageSize,
                cancellationToken);
            result.EnsureNoErrors();
            return Result.Ok(Map(result.Data));
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }
    }

    public IDisposable WatchPage(
        LevelGraphqlIdentity level,
        int page,
        int pageSize,
        Action<LeaderboardPageSnapshot> onNext,
        Action<Exception> onError)
    {
        return _gtrClient.WatchLeaderboardPage
            .Watch(level.XxHash, level.Hash, pageSize, page * pageSize)
            .Subscribe(new OperationObserver<IOperationResult<IWatchLeaderboardPageResult>>(
                result =>
                {
                    try
                    {
                        result.EnsureNoErrors();
                        onNext(Map(result.Data?.Query));
                    }
                    catch (Exception e)
                    {
                        onError(e);
                    }
                },
                onError));
    }

    private static LeaderboardPageSnapshot Map(ILeaderboardPageResult data)
    {
        IReadOnlyList<LeaderboardRecord> records = data?.Records?.Nodes
            .Select(record => new LeaderboardRecord
            {
                SteamId = record.User?.SteamId,
                SteamName = record.User?.SteamName,
                Time = record.Time,
                DateCreated = record.DateCreated
            })
            .ToList() ?? new List<LeaderboardRecord>();

        return new LeaderboardPageSnapshot
        {
            LevelName = data?.LevelItems?.Nodes.FirstOrDefault()?.Name,
            LevelPoints = data?.LevelPoints?.Nodes.FirstOrDefault()?.Points,
            TotalRecords = data?.Records?.TotalCount ?? 0,
            Records = records
        };
    }

    private static LeaderboardPageSnapshot Map(IWatchLeaderboardPage_Query data)
    {
        IReadOnlyList<LeaderboardRecord> records = data?.Records?.Nodes
            .Select(record => new LeaderboardRecord
            {
                SteamId = record.User?.SteamId,
                SteamName = record.User?.SteamName,
                Time = record.Time,
                DateCreated = record.DateCreated
            })
            .ToList() ?? new List<LeaderboardRecord>();

        return new LeaderboardPageSnapshot
        {
            LevelName = data?.LevelItems?.Nodes.FirstOrDefault()?.Name,
            LevelPoints = data?.LevelPoints?.Nodes.FirstOrDefault()?.Points,
            TotalRecords = data?.Records?.TotalCount ?? 0,
            Records = records
        };
    }
}

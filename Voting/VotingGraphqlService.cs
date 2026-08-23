using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using StrawberryShake;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;

namespace TNRD.Zeepkist.GTR.Voting;

public class VotingGraphqlService
{
    private readonly IGtrClient _gtrClient;

    public VotingGraphqlService(IGtrClient gtrClient)
    {
        _gtrClient = gtrClient;
    }

    public async UniTask<Result<VoteSummary>> GetVoteSummary(string xxHash, ulong steamId,
        CancellationToken cancellationToken)
    {
        try
        {
            IOperationResult<IGetLevelVoteSummaryResult> result =
                await _gtrClient.GetLevelVoteSummary.ExecuteAsync(
                    xxHash,
                    steamId.ToString(CultureInfo.InvariantCulture),
                    cancellationToken);
            result.EnsureNoErrors();

            var counts = new Dictionary<int, long>
            {
                [-2] = 0,
                [-1] = 0,
                [0] = 0,
                [1] = 0,
                [2] = 0
            };

            IReadOnlyList<IGetLevelVoteSummary_VoteCounts_GroupedAggregates> groupedAggregates =
                result.Data.VoteCounts?.GroupedAggregates;
            if (groupedAggregates != null)
            {
                foreach (IGetLevelVoteSummary_VoteCounts_GroupedAggregates aggregate in groupedAggregates)
                {
                    string key = aggregate.Keys?.FirstOrDefault();
                    string count = aggregate.DistinctCount?.UserId;
                    if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int voteValue) &&
                        voteValue is >= -2 and <= 2 &&
                        long.TryParse(count, NumberStyles.Integer, CultureInfo.InvariantCulture, out long voteCount))
                    {
                        counts[voteValue] = voteCount;
                    }
                }
            }

            int? currentVote = result.Data.CurrentVote?.Nodes.FirstOrDefault()?.Value;
            return Result.Ok(new VoteSummary(counts, currentVote));
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }
    }
}

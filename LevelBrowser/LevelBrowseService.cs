using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using StrawberryShake;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;

namespace TNRD.Zeepkist.GTR.LevelBrowser;

/// <summary>
/// Fetches Level Browser discovery pages from the GraphQL <c>levelItems</c> catalog using the pure
/// <see cref="LevelItemsBrowseQueryBuilder"/>. Returns a <see cref="Result{T}"/> of
/// <see cref="LevelBrowsePage"/>: success carries 0..n rows (each able to supply a Selection plus a
/// display <c>imageUrl</c>); failure is retryable so the UI can offer Retry. Callers own the loading
/// state while awaiting.
/// </summary>
public class LevelBrowseService
{
    private readonly IGtrClient _gtrClient;

    public LevelBrowseService(IGtrClient gtrClient)
    {
        _gtrClient = gtrClient;
    }

    /// <summary>Runs a browse for raw discovery inputs, building the default query internally.</summary>
    public UniTask<Result<LevelBrowsePage>> BrowseAsync(
        string name,
        string fileAuthor,
        int page,
        LevelBrowseSort sort = LevelBrowseSort.Newest,
        LevelBrowseDateRange dateRange = LevelBrowseDateRange.AnyTime,
        LevelBrowseTrackLength trackLength = LevelBrowseTrackLength.Any,
        LevelBrowseRating rating = LevelBrowseRating.Any,
        int minVotes = 0,
        int minPlays = 0,
        CancellationToken ct = default)
    {
        return BrowseAsync(
            LevelItemsBrowseQueryBuilder.Build(
                name,
                fileAuthor,
                page,
                sort: sort,
                dateRange: dateRange,
                trackLength: trackLength,
                rating: rating,
                minVotes: minVotes,
                minPlays: minPlays,
                nowUtc: DateTimeOffset.UtcNow),
            ct);
    }

    /// <summary>Runs a browse for an already-built query.</summary>
    public async UniTask<Result<LevelBrowsePage>> BrowseAsync(
        LevelItemsBrowseQuery query,
        CancellationToken ct = default)
    {
        try
        {
            LevelItemFilter filter = BuildFilter(query);
            IReadOnlyList<LevelItemsOrderBy> orderBy = BuildOrderBy(query.Sort);

            IOperationResult<IBrowseLevelItemsResult> result =
                await _gtrClient.BrowseLevelItems.ExecuteAsync(filter, orderBy, query.First, query.Offset, ct);

            try
            {
                result.EnsureNoErrors();
            }
            catch (Exception e)
            {
                return Result.Fail(new ExceptionalError(e));
            }

            IBrowseLevelItems_LevelItems connection = result.Data?.LevelItems;
            if (connection == null)
                return Result.Ok(new LevelBrowsePage(Array.Empty<LevelBrowseRow>(), 0));

            var rows = new List<LevelBrowseRow>(connection.Nodes.Count);
            foreach (IBrowseLevelItems_LevelItems_Nodes node in connection.Nodes)
            {
                rows.Add(new LevelBrowseRow(
                    node.FileUid,
                    WorkshopIdParser.Parse(node.WorkshopId),
                    node.Name,
                    node.FileAuthor,
                    node.ImageUrl));
            }

            return Result.Ok(new LevelBrowsePage(rows, connection.TotalCount));
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }
    }

    internal static LevelItemFilter BuildFilter(LevelItemsBrowseQuery query)
    {
        var level = new LevelFilter
        {
            PubliclyVisible = new BooleanFilter { EqualTo = LevelItemsBrowseQuery.HygienePubliclyVisibleEqualTo }
        };

        ApplyEngagement(level, query);

        var filter = new LevelItemFilter
        {
            Deleted = new BooleanFilter { EqualTo = LevelItemsBrowseQuery.HygieneDeletedEqualTo },
            Level = level
        };

        if (!string.IsNullOrEmpty(query.NameIncludesInsensitive))
            filter.Name = new StringFilter { IncludesInsensitive = query.NameIncludesInsensitive };

        if (!string.IsNullOrEmpty(query.FileAuthorIncludesInsensitive))
            filter.FileAuthor = new StringFilter { IncludesInsensitive = query.FileAuthorIncludesInsensitive };

        if (query.TimeMin.HasValue || query.TimeMax.HasValue)
        {
            var time = new FloatFilter();
            if (query.TimeMin.HasValue)
                time.GreaterThanOrEqualTo = query.TimeMin.Value;
            if (query.TimeMax.HasValue)
                time.LessThanOrEqualTo = query.TimeMax.Value;
            filter.ValidationTimeAuthor = time;
        }

        if (query.DateCreatedAfter.HasValue)
        {
            filter.DateCreated = new DatetimeFilter
            {
                GreaterThanOrEqualTo = FormatDatetime(query.DateCreatedAfter.Value)
            };
        }

        return filter;
    }

    private static void ApplyEngagement(LevelFilter level, LevelItemsBrowseQuery query)
    {
        bool hasVotes =
            query.Rating != LevelBrowseRating.Any ||
            query.MinVotes.HasValue;

        if (hasVotes)
        {
            var aggregates = new VoteAggregatesFilter();

            switch (query.Rating)
            {
                case LevelBrowseRating.WellRated:
                    aggregates.Average = new VoteAverageAggregateFilter
                    {
                        Value = new BigFloatFilter { GreaterThan = "0" }
                    };
                    break;
                case LevelBrowseRating.TopRated:
                    aggregates.Sum = new VoteSumAggregateFilter
                    {
                        Value = new BigIntFilter
                        {
                            GreaterThanOrEqualTo = LevelItemsBrowseQuery.TopRatedNetScore.ToString(CultureInfo.InvariantCulture)
                        }
                    };
                    break;
            }

            if (query.MinVotes.HasValue)
            {
                aggregates.DistinctCount = new VoteDistinctCountAggregateFilter
                {
                    UserId = new BigIntFilter
                    {
                        GreaterThanOrEqualTo = query.MinVotes.Value.ToString(CultureInfo.InvariantCulture)
                    }
                };
            }

            level.Votes = new LevelToManyVoteFilter { Aggregates = aggregates };
        }

        if (query.MinPlays.HasValue)
        {
            level.Records = new LevelToManyRecordFilter
            {
                Aggregates = new RecordAggregatesFilter
                {
                    DistinctCount = new RecordDistinctCountAggregateFilter
                    {
                        Id = new BigIntFilter
                        {
                            GreaterThanOrEqualTo = query.MinPlays.Value.ToString(CultureInfo.InvariantCulture)
                        }
                    }
                }
            };
        }
    }

    internal static IReadOnlyList<LevelItemsOrderBy> BuildOrderBy(LevelBrowseSort sort)
    {
        switch (sort)
        {
            case LevelBrowseSort.Oldest:
                return new[] { LevelItemsOrderBy.DateCreatedAsc };
            case LevelBrowseSort.NameAsc:
                return new[] { LevelItemsOrderBy.NameAsc };
            case LevelBrowseSort.NameDesc:
                return new[] { LevelItemsOrderBy.NameDesc };
            case LevelBrowseSort.Shortest:
                return new[] { LevelItemsOrderBy.ValidationTimeAuthorAsc };
            case LevelBrowseSort.Longest:
                return new[] { LevelItemsOrderBy.ValidationTimeAuthorDesc };
            default:
                return new[] { LevelItemsOrderBy.DateCreatedDesc };
        }
    }

    private static string FormatDatetime(DateTimeOffset value)
    {
        return value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
    }
}

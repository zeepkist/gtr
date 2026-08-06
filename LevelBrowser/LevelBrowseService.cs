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
    /// <summary>Max users pulled into the one-time author cache.</summary>
    public const int AuthorCacheLimit = 10000;

    private readonly IGtrClient _gtrClient;
    private readonly List<AuthorSuggestion> _authorCache = new();
    private readonly HashSet<string> _authorCacheIds = new(StringComparer.Ordinal);
    private bool _authorCacheFullyLoaded;
    private bool _authorCacheLoading;
    private UniTaskCompletionSource _authorCacheWait = new();

    public LevelBrowseService(IGtrClient gtrClient)
    {
        _gtrClient = gtrClient;
        // Already completed until a load starts, so waiters don't hang before the first prefetch.
        _authorCacheWait.TrySetResult();
    }

    /// <summary>Default suggestion page size for <see cref="SearchAuthorsAsync"/>.</summary>
    public const int DefaultAuthorSearchLimit = 12;

    /// <summary>Runs a browse for raw discovery inputs, building the default query internally.</summary>
    public UniTask<Result<LevelBrowsePage>> BrowseAsync(
        string name,
        string fileAuthor,
        int page,
        LevelBrowseSort sort = LevelBrowseSort.Newest,
        LevelBrowseDateRange dateRange = LevelBrowseDateRange.AnyTime,
        LevelBrowseTrackLength trackLength = LevelBrowseTrackLength.Any,
        LevelBrowseRating rating = LevelBrowseRating.Any,
        bool ownLevelsOnly = false,
        bool withoutMyPersonalBest = false,
        bool withoutRecords = false,
        string ownerSteamId = null,
        string authorUserId = null,
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
                ownLevelsOnly: ownLevelsOnly,
                withoutMyPersonalBest: withoutMyPersonalBest,
                withoutRecords: withoutRecords,
                ownerSteamId: ownerSteamId,
                authorUserId: authorUserId,
                nowUtc: DateTimeOffset.UtcNow),
            ct);
    }

    /// <summary>
    /// Prefetches GTR users (steam id + steam name) once into an in-memory cache. Safe to call
    /// repeatedly; concurrent callers share one load.
    /// </summary>
    public async UniTask EnsureAuthorCacheAsync(CancellationToken ct = default)
    {
        if (_authorCacheFullyLoaded)
            return;

        if (_authorCacheLoading)
        {
            await _authorCacheWait.Task;
            return;
        }

        _authorCacheLoading = true;
        _authorCacheWait = new UniTaskCompletionSource();
        try
        {
            Result<IReadOnlyList<AuthorSuggestion>> result = await FetchAuthorsAsync(
                namePart: null,
                limit: AuthorCacheLimit,
                ct);
            if (result.IsSuccess)
            {
                MergeAuthors(result.Value);
                _authorCacheFullyLoaded = true;
            }
        }
        finally
        {
            _authorCacheLoading = false;
            _authorCacheWait.TrySetResult();
        }
    }

    /// <summary>True when the full author cache has finished loading successfully.</summary>
    public bool IsAuthorCacheReady => _authorCacheFullyLoaded;

    /// <summary>
    /// Filters the in-memory author cache synchronously. Returns false when the cache has no
    /// entries yet (caller should fall back to <see cref="SearchAuthorsAsync"/>).
    /// </summary>
    public bool TryFilterAuthors(
        string namePart,
        int limit,
        out IReadOnlyList<AuthorSuggestion> matches)
    {
        matches = Array.Empty<AuthorSuggestion>();
        if (_authorCache.Count == 0)
            return false;

        if (string.IsNullOrWhiteSpace(namePart) || namePart.Trim().Length < 2)
            return true;

        matches = FilterCache(namePart.Trim(), limit);
        return true;
    }

    /// <summary>
    /// Searches GTR users by case-insensitive <c>steamName</c> substring for the "Uploaded by"
    /// autocomplete. Prefers the in-memory cache; falls back to a live GraphQL query and merges
    /// hits into the cache.
    /// </summary>
    public async UniTask<Result<IReadOnlyList<AuthorSuggestion>>> SearchAuthorsAsync(
        string namePart,
        int limit = DefaultAuthorSearchLimit,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(namePart) || namePart.Trim().Length < 2)
            return Result.Ok<IReadOnlyList<AuthorSuggestion>>(Array.Empty<AuthorSuggestion>());

        if (limit < 1)
            limit = DefaultAuthorSearchLimit;

        string needle = namePart.Trim();

        if (_authorCacheFullyLoaded || _authorCache.Count > 0)
        {
            IReadOnlyList<AuthorSuggestion> local = FilterCache(needle, limit);
            if (_authorCacheFullyLoaded || local.Count > 0)
                return Result.Ok(local);
        }

        Result<IReadOnlyList<AuthorSuggestion>> live = await FetchAuthorsAsync(needle, limit, ct);
        if (live.IsFailed)
            return live;

        MergeAuthors(live.Value);
        return Result.Ok(FilterCache(needle, limit));
    }

    private IReadOnlyList<AuthorSuggestion> FilterCache(string needle, int limit)
    {
        var list = new List<AuthorSuggestion>(Math.Min(limit, 16));
        foreach (AuthorSuggestion author in _authorCache)
        {
            if (author.SteamName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            list.Add(author);
            if (list.Count >= limit)
                break;
        }

        return list;
    }

    private void MergeAuthors(IReadOnlyList<AuthorSuggestion> authors)
    {
        foreach (AuthorSuggestion author in authors)
        {
            if (string.IsNullOrWhiteSpace(author.SteamId) || string.IsNullOrWhiteSpace(author.SteamName))
                continue;

            if (!_authorCacheIds.Add(author.SteamId))
                continue;

            _authorCache.Add(author);
        }
    }

    private async UniTask<Result<IReadOnlyList<AuthorSuggestion>>> FetchAuthorsAsync(
        string namePart,
        int limit,
        CancellationToken ct)
    {
        try
        {
            var filter = new UserFilter();
            if (!string.IsNullOrEmpty(namePart))
                filter.SteamName = new StringFilter { IncludesInsensitive = namePart };

            IOperationResult<ISearchUsersByNameResult> result =
                await _gtrClient.SearchUsersByName.ExecuteAsync(filter, limit, ct);

            try
            {
                result.EnsureNoErrors();
            }
            catch (Exception e)
            {
                return Result.Fail(new ExceptionalError(e));
            }

            ISearchUsersByName_Users connection = result.Data?.Users;
            if (connection == null)
                return Result.Ok<IReadOnlyList<AuthorSuggestion>>(Array.Empty<AuthorSuggestion>());

            var list = new List<AuthorSuggestion>(connection.Nodes.Count);
            foreach (ISearchUsersByName_Users_Nodes node in connection.Nodes)
            {
                if (string.IsNullOrWhiteSpace(node.SteamId) || string.IsNullOrWhiteSpace(node.SteamName))
                    continue;

                list.Add(new AuthorSuggestion(node.SteamId, node.SteamName));
            }

            return Result.Ok<IReadOnlyList<AuthorSuggestion>>(list);
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }
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

        ApplyRating(level, query.Rating);

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

        if (!string.IsNullOrEmpty(query.AuthorIdEqualTo))
            filter.AuthorId = new BigIntFilter { EqualTo = query.AuthorIdEqualTo };

        if (!string.IsNullOrEmpty(query.ExcludePersonalBestSteamId))
        {
            level.PersonalBestGlobals = new LevelToManyPersonalBestGlobalFilter
            {
                None = new PersonalBestGlobalFilter
                {
                    User = new UserFilter
                    {
                        SteamId = new BigIntFilter { EqualTo = query.ExcludePersonalBestSteamId }
                    }
                }
            };
        }

        if (query.RequireNoRecords)
            level.RecordsExist = false;

        return filter;
    }

    private static void ApplyRating(LevelFilter level, LevelBrowseRating rating)
    {
        switch (rating)
        {
            case LevelBrowseRating.WellRated:
                level.Votes = new LevelToManyVoteFilter
                {
                    Aggregates = new VoteAggregatesFilter
                    {
                        Average = new VoteAverageAggregateFilter
                        {
                            Value = new BigFloatFilter { GreaterThan = "0" }
                        }
                    }
                };
                return;
            case LevelBrowseRating.TopRated:
                level.Votes = new LevelToManyVoteFilter
                {
                    Aggregates = new VoteAggregatesFilter
                    {
                        Sum = new VoteSumAggregateFilter
                        {
                            Value = new BigIntFilter
                            {
                                GreaterThanOrEqualTo = LevelItemsBrowseQuery.TopRatedNetScore.ToString(CultureInfo.InvariantCulture)
                            }
                        }
                    }
                };
                return;
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

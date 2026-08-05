using System;
using System.Collections.Generic;
using System.Threading;
using StrawberryShake;
using TNRD.Zeepkist.GTR.GraphQL;
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
        CancellationToken ct = default)
    {
        return BrowseAsync(LevelItemsBrowseQueryBuilder.Build(name, fileAuthor, page), ct);
    }

    /// <summary>Runs a browse for an already-built query.</summary>
    public async UniTask<Result<LevelBrowsePage>> BrowseAsync(
        LevelItemsBrowseQuery query,
        CancellationToken ct = default)
    {
        try
        {
            // includesInsensitive: "" matches every (non-null) string, i.e. "no filter".
            string name = query.NameIncludesInsensitive ?? string.Empty;
            string fileAuthor = query.FileAuthorIncludesInsensitive ?? string.Empty;

            IOperationResult<IBrowseLevelItemsResult> result =
                await _gtrClient.BrowseLevelItems.ExecuteAsync(name, fileAuthor, query.First, query.Offset, ct);

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
}

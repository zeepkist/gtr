using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Api;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.Level;
using ZeepSDK.Messaging;

namespace TNRD.Zeepkist.GTR.Favourites;

public class FavouriteService
{
    private readonly ILogger<FavouriteService> _logger;
    private readonly ApiHttpClient _apiHttpClient;

    public FavouriteService(ILogger<FavouriteService> logger, ApiHttpClient apiHttpClient)
    {
        _logger = logger;
        _apiHttpClient = apiHttpClient;
    }

    public void Favourite()
    {
        UpdateFavouriteAsync(true).Forget();
    }

    public void Unfavourite()
    {
        UpdateFavouriteAsync(false).Forget();
    }

    private async UniTaskVoid UpdateFavouriteAsync(bool favourite)
    {
        string currentHash = LevelApi.CurrentHashV2?.Hash;

        if (string.IsNullOrEmpty(currentHash))
        {
            _logger.LogError("Unable to {Action} because current level hash is empty",
                favourite ? "favourite" : "unfavourite");
            OnFail(favourite);
            return;
        }

        string endpoint = favourite ? "favourite/add" : "favourite/remove";
        using HttpResponseMessage response = await _apiHttpClient.PostAsync(
            endpoint,
            new FavouriteResource { Hash = currentHash }
        );

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to {Action} level", favourite ? "favourite" : "unfavourite");
            OnFail(favourite);
            return;
        }

        MessengerApi.LogSuccess(favourite ? "Level favourited" : "Level unfavourited");
    }

    private static void OnFail(bool favourite)
    {
        MessengerApi.LogError(favourite ? "Favourite failed" : "Unfavourite failed");
    }
}

using Microsoft.Extensions.DependencyInjection;
using TNRD.Zeepkist.GTR.Favourites;
using TNRD.Zeepkist.GTR.Utilities;
using ZeepSDK.ChatCommands;

namespace TNRD.Zeepkist.GTR.Commands.Favourites;

public class FavouriteCommand : ILocalChatCommand
{
    private readonly FavouriteService _favouriteService;

    public string Prefix => "/";
    public string Command => "fav";
    public string Description => "Adds the current level to your ZeepCentraal favourites";

    public FavouriteCommand()
    {
        _favouriteService = ServiceHelper.Instance.GetRequiredService<FavouriteService>();
    }

    public void Handle(string arguments)
    {
        _favouriteService.Favourite();
    }
}

using Microsoft.Extensions.DependencyInjection;
using TNRD.Zeepkist.GTR.Favourites;
using TNRD.Zeepkist.GTR.Utilities;
using ZeepSDK.ChatCommands;

namespace TNRD.Zeepkist.GTR.Commands.Favourites;

public class UnfavouriteCommand : ILocalChatCommand
{
    private readonly FavouriteService _favouriteService;

    public string Prefix => "/";
    public string Command => "unfav";
    public string Description => "Removes the current level from your ZeepCentraal favourites";

    public UnfavouriteCommand()
    {
        _favouriteService = ServiceHelper.Instance.GetRequiredService<FavouriteService>();
    }

    public void Handle(string arguments)
    {
        _favouriteService.Unfavourite();
    }
}

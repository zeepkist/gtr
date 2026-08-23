using Newtonsoft.Json;

namespace TNRD.Zeepkist.GTR.Api;

public class FavouriteResource
{
    [JsonProperty("hash")]
    public string Hash { get; set; } = null!;
}

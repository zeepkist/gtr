using Newtonsoft.Json;
using TNRD.Zeepkist.GTR.Api;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class FavouriteResourceTests
{
    [Fact]
    public void SerializesExactBackendContract()
    {
        var resource = new FavouriteResource
        {
            Hash = "0123456789ABCDEF0123456789ABCDEF"
        };

        string json = JsonConvert.SerializeObject(resource);

        Assert.Equal("{\"hash\":\"0123456789ABCDEF0123456789ABCDEF\"}", json);
    }
}

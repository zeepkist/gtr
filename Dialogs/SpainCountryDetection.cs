using System;
using Newtonsoft.Json;

namespace TNRD.Zeepkist.GTR.Dialogs;

internal static class SpainCountryDetection
{
    public static bool IsSpanishResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return false;

        try
        {
            IpCountryResponse country = JsonConvert.DeserializeObject<IpCountryResponse>(response);
            return string.Equals(country?.Country, "ES", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed class IpCountryResponse
    {
        [JsonProperty("country")]
        public string Country { get; set; }
    }
}

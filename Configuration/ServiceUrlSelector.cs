namespace TNRD.Zeepkist.GTR.Configuration;

internal static class ServiceUrlSelector
{
    public static string Select(
        bool useLocalDevelopment,
        bool useAlternativeDomains,
        string productionUrl,
        string alternativeUrl,
        string localDevelopmentUrl)
    {
        if (useLocalDevelopment)
            return localDevelopmentUrl;

        return useAlternativeDomains ? alternativeUrl : productionUrl;
    }
}

namespace HomeSpeaker.Server2;

public static class FeatureFlags
{
    public static bool TemperatureEnabled(IConfiguration configuration) =>
        !string.IsNullOrEmpty(configuration["Temperature:ApiBaseUrl"]);

    public static bool BloodSugarEnabled(IConfiguration configuration) =>
        !string.IsNullOrEmpty(configuration["NIGHTSCOUT_URL"]);
}

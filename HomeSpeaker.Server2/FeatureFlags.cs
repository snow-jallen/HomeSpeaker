using System.Globalization;

namespace HomeSpeaker.Server2;

public static class FeatureFlags
{
    public static bool TemperatureEnabled(IConfiguration configuration) =>
        !string.IsNullOrEmpty(configuration["Temperature:ApiBaseUrl"]);

    public static bool BloodSugarEnabled(IConfiguration configuration) =>
        !string.IsNullOrEmpty(configuration["NIGHTSCOUT_URL"]);

    public static bool ForecastEnabled(IConfiguration configuration) =>
        double.TryParse(configuration["Forecast:Latitude"], NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
        double.TryParse(configuration["Forecast:Longitude"], NumberStyles.Float, CultureInfo.InvariantCulture, out _);
}

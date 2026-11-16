using System.Globalization;

namespace HomeSpeaker.Server2;

public static class FeatureFlags
{
    /// <summary>On with the Govee cloud URL set, or with Bluetooth as the source.</summary>
    public static bool TemperatureEnabled(IConfiguration configuration) =>
        !string.IsNullOrEmpty(configuration["Temperature:ApiBaseUrl"]) ||
        Services.BleTemperature.TemperatureSource.IsBluetooth(configuration);

    public static bool BloodSugarEnabled(IConfiguration configuration) =>
        !string.IsNullOrEmpty(configuration["NIGHTSCOUT_URL"]);

    public static bool ForecastEnabled(IConfiguration configuration) =>
        double.TryParse(configuration["Forecast:Latitude"], NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
        double.TryParse(configuration["Forecast:Longitude"], NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    public static bool AnchorsEnabled(IConfiguration configuration) =>
        string.Equals(configuration["Features:Anchors"], "true", StringComparison.OrdinalIgnoreCase);

    public static bool PushEnabled(IConfiguration configuration) =>
        !string.IsNullOrEmpty(configuration["PushNotifications:Apns:Topic"]) &&
        !string.IsNullOrEmpty(configuration["PushNotifications:Apns:TeamId"]) &&
        !string.IsNullOrEmpty(configuration["PushNotifications:Apns:KeyId"]) &&
        (!string.IsNullOrEmpty(configuration["PushNotifications:Apns:PrivateKeyBase64"]) ||
         !string.IsNullOrEmpty(configuration["PushNotifications:Apns:PrivateKeyPath"]));
}

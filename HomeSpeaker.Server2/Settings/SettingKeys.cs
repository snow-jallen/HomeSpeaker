namespace HomeSpeaker.Server2.Settings;

/// <summary>The configuration keys the settings page and <c>/api/settings</c> may read and write.</summary>
public static class SettingKeys
{
    public const string ForecastLatitude = "Forecast:Latitude";
    public const string ForecastLongitude = "Forecast:Longitude";

    public const string PhotoSource = "Photos:Source";
    public const string ImmichBaseUrl = "Immich:BaseUrl";
    public const string ImmichApiKey = "Immich:ApiKey";
    public const string ImmichAlbumId = "Immich:AlbumId";
    public const string SlideSeconds = "Immich:SlideSeconds";
    public const string IdleSeconds = "Ambient:IdleSeconds";

    public const string TemperatureUnit = "Units:Temperature";

    public const string NightscoutUrl = "NIGHTSCOUT_URL";

    public const string TemperatureApiBaseUrl = "Temperature:ApiBaseUrl";
    public const string TemperatureApiKey = "Temperature:ApiKey";
    public const string TemperatureRooms = "Temperature:Rooms";
    public const string TemperatureOutsideRoom = "Temperature:OutsideRoom";
    public const string TemperatureIndoorRoom = "Temperature:IndoorRoom";
    public const string TemperatureWindowAdvice = "Temperature:WindowAdvice";
    public const string TemperatureWindowMinOutside = "Temperature:WindowMinOutside";
    public const string TemperatureThreshold = "TemperatureThreshold";

    /// <summary>
    /// What the Govee checkbox writes. The service resolves relative paths ("user/devices") against it, so it
    /// carries the API's router path and a trailing slash.
    /// </summary>
    public const string GoveeApiBaseUrl = "https://openapi.api.govee.com/router/api/v1/";

    public const string Anchors = "Features:Anchors";

    /// <summary>Every editable key, its label and whether it is a write-only secret, in page order.</summary>
    public static IReadOnlyList<(string Key, string Label, bool IsSecret)> All { get; } =
    [
        (ForecastLatitude, "Latitude", false),
        (ForecastLongitude, "Longitude", false),
        (PhotoSource, "Photo source", false),
        (ImmichBaseUrl, "Immich URL", false),
        (ImmichApiKey, "Immich API key", true),
        (ImmichAlbumId, "Immich album", false),
        (SlideSeconds, "Seconds per photo", false),
        (IdleSeconds, "Idle seconds before slideshow", false),
        (TemperatureUnit, "Temperature unit", false),
        (NightscoutUrl, "Nightscout URL", false),
        (TemperatureApiBaseUrl, "Govee API URL", false),
        (TemperatureApiKey, "Govee API key", true),
        (TemperatureRooms, "Rooms", false),
        (TemperatureOutsideRoom, "Outside room", false),
        (TemperatureIndoorRoom, "Indoor room", false),
        (TemperatureWindowAdvice, "Window advice", false),
        (TemperatureWindowMinOutside, "Minimum outside temperature", false),
        (TemperatureThreshold, "Window threshold", false),
        (Anchors, "Anchors", false),
    ];

    /// <summary>
    /// True for keys that are only read when HomeSpeaker starts (the photo source and Immich client, the feature
    /// modules that register endpoints and workers), so a change needs a restart to apply.
    /// </summary>
    public static bool RequiresRestart(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key.StartsWith("Immich:", StringComparison.OrdinalIgnoreCase)
            || restartKeys.Contains(key);
    }

    private static readonly HashSet<string> restartKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        PhotoSource,
        NightscoutUrl,
        TemperatureApiBaseUrl,
        TemperatureApiKey,
        Anchors,
    };

    /// <summary>Keys that must be whole numbers of at least 1.</summary>
    public static IReadOnlySet<string> PositiveIntegers { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        SlideSeconds,
        IdleSeconds,
    };

    /// <summary>Keys that must be numbers, with their inclusive range.</summary>
    public static IReadOnlyDictionary<string, (double Min, double Max)> Numbers { get; } = new Dictionary<string, (double Min, double Max)>(StringComparer.OrdinalIgnoreCase)
    {
        [ForecastLatitude] = (-90, 90),
        [ForecastLongitude] = (-180, 180),
        [TemperatureWindowMinOutside] = (-100, 150),
        [TemperatureThreshold] = (0, 100),
    };
}

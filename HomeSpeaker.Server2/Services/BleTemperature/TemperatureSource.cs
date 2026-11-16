using System.Globalization;
using HomeSpeaker.Server2.Settings;

namespace HomeSpeaker.Server2.Services.BleTemperature;

/// <summary>
/// Where room temperatures come from: the Govee cloud (default) or Bluetooth, where every speaker scans and sends what
/// it hears to one hub speaker (<c>Temperature:HubUrl</c>, empty on the hub itself). Read live, so a settings change
/// applies at once.
/// </summary>
public static class TemperatureSource
{
    public const string Cloud = "cloud";
    public const string Bluetooth = "bluetooth";

    private const int DefaultScanSeconds = 10;
    private const int DefaultScanEverySeconds = 60;
    private const int MaxScanEverySeconds = 3600;

    /// <summary>Where docker-compose.bluetooth.yml mounts the host's /etc/hostname, so readings name the speaker.</summary>
    public const string HostHostnamePath = "/etc/host-hostname";

    public static bool IsBluetooth(IConfiguration configuration) =>
        string.Equals(configuration[SettingKeys.TemperatureSource]?.Trim(), Bluetooth, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when Bluetooth is the source and this speaker aggregates and serves the readings.</summary>
    public static bool IsHub(IConfiguration configuration) => IsBluetooth(configuration) && HubUrl(configuration) is null;

    /// <summary>The hub's base URL with a trailing slash, or null when unset (this speaker is the hub) or not http(s).</summary>
    public static Uri? HubUrl(IConfiguration configuration)
    {
        var text = configuration[SettingKeys.TemperatureHubUrl]?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (!text.EndsWith('/'))
        {
            text += "/";
        }

        return Uri.TryCreate(text, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) ? uri : null;
    }

    /// <summary>
    /// How long each scan burst lasts and how often one starts. Scanning in bursts leaves the radio to Wi-Fi in
    /// between: on a Pi the two share one chip.
    /// </summary>
    public static (TimeSpan Burst, TimeSpan Every) ScanTiming(IConfiguration configuration)
    {
        var every = Math.Min(readSeconds(configuration, SettingKeys.TemperatureScanEverySeconds, DefaultScanEverySeconds), MaxScanEverySeconds);
        var burst = Math.Min(readSeconds(configuration, SettingKeys.TemperatureScanSeconds, DefaultScanSeconds), every);
        return (TimeSpan.FromSeconds(burst), TimeSpan.FromSeconds(every));
    }

    private static int readSeconds(IConfiguration configuration, string key, int fallback) =>
        int.TryParse(configuration[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0 ? value : fallback;

    /// <summary>
    /// The name this speaker reports its readings under: the host's name when the overlay provides it (a container's
    /// own name is a random id that changes on every recreate), else <paramref name="machineName"/>.
    /// </summary>
    public static string Reporter(string? hostHostname, string machineName) =>
        hostHostname?.Trim() is { Length: > 0 } host ? host : machineName;
}

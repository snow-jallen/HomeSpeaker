using HomeSpeaker.Server2.Settings;

namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>Where this device's anchors live: here, or on another HomeSpeaker (read live, so a settings change applies at once).</summary>
public static class AnchorSourceSettings
{
    public const string Local = "local";
    public const string Remote = "remote";

    public static bool IsRemote(IConfiguration configuration) =>
        string.Equals(configuration[SettingKeys.AnchorsSource]?.Trim(), Remote, StringComparison.OrdinalIgnoreCase);

    /// <summary>The anchor home's base URL with a trailing slash, or null when it isn't a usable http(s) URL.</summary>
    public static Uri? RemoteUrl(IConfiguration configuration)
    {
        var text = configuration[SettingKeys.AnchorsRemoteUrl]?.Trim();
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
}

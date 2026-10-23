using HomeSpeaker.Server2.Settings;

namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>
/// <c>IMMICH_API_KEY</c> is an older name for <c>Immich:ApiKey</c>. Set in the environment (or on the command line)
/// it acts like an environment override: it beats a key saved on the settings page, but not an
/// <c>Immich__ApiKey</c> environment variable. Anywhere else it only fills in a missing key.
/// </summary>
public static class ImmichOptionsFallback
{
    public const string ApiKeyConfigKey = "IMMICH_API_KEY";

    private const string SectionKeyConfigKey = "Immich:ApiKey";

    public static void Apply(ImmichOptions options, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (aliasBeatsSectionKey(configuration) || string.IsNullOrWhiteSpace(options.ApiKey))
        {
            options.ApiKey = configuration[ApiKeyConfigKey];
        }
    }

    /// <summary>The Immich API key HomeSpeaker uses, by the same rule as <see cref="Apply"/>.</summary>
    public static string? EffectiveApiKey(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration[SectionKeyConfigKey];
        return aliasBeatsSectionKey(configuration) || string.IsNullOrWhiteSpace(section) ? configuration[ApiKeyConfigKey] : section;
    }

    /// <summary>True when a non-empty <c>IMMICH_API_KEY</c> comes from a source above the database settings.</summary>
    public static bool AliasIsSetAboveDatabase(IConfiguration configuration) =>
        SettingsForm.TryGetAboveDatabase(configuration, ApiKeyConfigKey, out var alias) && !string.IsNullOrWhiteSpace(alias);

    private static bool aliasBeatsSectionKey(IConfiguration configuration) =>
        AliasIsSetAboveDatabase(configuration) && !SettingsForm.TryGetAboveDatabase(configuration, SectionKeyConfigKey, out _);
}

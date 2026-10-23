namespace HomeSpeaker.Server2.Settings;

/// <summary>
/// The forgot-PIN switch: with <c>Admin:ResetPin</c> set to "true" (env <c>Admin__ResetPin=true</c>), startup deletes
/// the saved <c>Admin:PinHash</c> from the settings database so a new PIN can be chosen.
/// </summary>
public static class AdminPinReset
{
    public const string ConfigKey = "Admin:ResetPin";

    /// <summary>Returns true when a reset was requested and the saved PIN was cleared.</summary>
    public static bool Apply(
        IConfiguration configuration,
        SqliteSettingsStore? store,
        DatabaseSettingsConfigurationProvider? provider,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
        if (store is null || !string.Equals(configuration[ConfigKey]?.Trim(), "true", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        store.Save(new Dictionary<string, string?> { [AdminPin.ConfigKey] = null });
        provider?.Reload();
#pragma warning disable CA1848, CA2254 // one-off startup message
        logger.LogWarning("Admin PIN cleared by Admin:ResetPin; remove the setting and restart.");
#pragma warning restore CA1848, CA2254
        return true;
    }
}

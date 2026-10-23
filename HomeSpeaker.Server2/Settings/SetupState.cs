namespace HomeSpeaker.Server2.Settings;

/// <summary>Decides whether the first-run wizard should be offered.</summary>
public static class SetupState
{
    public const string CompletedKey = "Setup:Completed";

    /// <summary>
    /// True until setup is finished or a location is configured (so installs that were configured before the
    /// wizard existed never see it).
    /// </summary>
    public static bool IsNeeded(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return !string.Equals(configuration[CompletedKey], "true", StringComparison.OrdinalIgnoreCase)
            && !FeatureFlags.ForecastEnabled(configuration);
    }
}

using HomeSpeaker.Server2.Endpoints;

namespace HomeSpeaker.Server2;

public static class FeatureModules
{
    /// <summary>
    /// Maps the endpoints of each optional feature module, but only when its config flag is on,
    /// so a household that doesn't use a feature has no routes for it.
    /// </summary>
    public static WebApplication MapFeatureModules(this WebApplication app)
    {
        var config = app.Configuration;
        if (FeatureFlags.TemperatureEnabled(config))
        {
            app.MapTemperatureApi();
        }

        if (FeatureFlags.BloodSugarEnabled(config))
        {
            app.MapBloodSugarApi();
        }

        if (FeatureFlags.ForecastEnabled(config))
        {
            app.MapForecastApi();
        }

        if (FeatureFlags.AnchorsEnabled(config))
        {
            app.MapAnchorApi();
        }

        if (FeatureFlags.PushEnabled(config))
        {
            app.MapPushApi();
        }

        return app;
    }
}

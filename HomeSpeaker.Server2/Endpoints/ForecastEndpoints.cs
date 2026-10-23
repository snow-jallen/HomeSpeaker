using HomeSpeaker.Server2.Services;

namespace HomeSpeaker.Server2.Endpoints;

public static class ForecastEndpoints
{
    public static IEndpointRouteBuilder MapForecastApi(this IEndpointRouteBuilder app)
    {
        // Forecast API endpoint
        app.MapGet("/api/forecast", async (IForecastService forecastService, CancellationToken cancellationToken) =>
        {
            try
            {
                var forecastStatus = await forecastService.GetForecastStatusAsync(cancellationToken);
                return Results.Ok(forecastStatus);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get forecast data: {ex.Message}");
            }
        });

        // Forecast cache management endpoints
        app.MapDelete("/api/forecast/cache", async (IForecastService forecastService) =>
        {
            try
            {
                await forecastService.ClearCacheAsync();
                return Results.Ok(new { message = "Forecast cache cleared successfully" });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to clear forecast cache: {ex.Message}");
            }
        });

        app.MapPost("/api/forecast/refresh", async (IForecastService forecastService, CancellationToken cancellationToken) =>
        {
            try
            {
                var forecastStatus = await forecastService.RefreshAsync(cancellationToken);
                return Results.Ok(forecastStatus);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to refresh forecast data: {ex.Message}");
            }
        });

        return app;
    }
}

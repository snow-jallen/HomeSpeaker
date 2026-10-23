using HomeSpeaker.Server2.Services;

namespace HomeSpeaker.Server2.Endpoints;

public static class TemperatureEndpoints
{
    public static IEndpointRouteBuilder MapTemperatureApi(this IEndpointRouteBuilder app)
    {
        // Temperature API endpoint
        app.MapGet("/api/temperature", async (ITemperatureService temperatureService, CancellationToken cancellationToken) =>
        {
            try
            {
                var temperatureStatus = await temperatureService.GetTemperatureStatusAsync(cancellationToken);
                return Results.Ok(temperatureStatus);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get temperature data: {ex.Message}");
            }
        });

        // Read-only diagnostic: what each matched sensor reports (e.g. whether battery is available at all).
        app.MapGet("/api/temperature/capabilities", async (ITemperatureService temperatureService, CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await temperatureService.GetSensorDiagnosticsAsync(cancellationToken));
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get sensor capabilities: {ex.Message}");
            }
        });

        // Temperature cache management endpoints
        app.MapDelete("/api/temperature/cache", async (ITemperatureService temperatureService) =>
        {
            try
            {
                await temperatureService.ClearCacheAsync();
                return Results.Ok(new { message = "Temperature cache cleared successfully" });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to clear temperature cache: {ex.Message}");
            }
        });

        app.MapPost("/api/temperature/refresh", async (ITemperatureService temperatureService, CancellationToken cancellationToken) =>
        {
            try
            {
                var temperatureStatus = await temperatureService.RefreshAsync(cancellationToken);
                return Results.Ok(temperatureStatus);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to refresh temperature data: {ex.Message}");
            }
        });

        return app;
    }
}

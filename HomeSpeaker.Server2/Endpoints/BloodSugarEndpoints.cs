using HomeSpeaker.Server2.Services;

namespace HomeSpeaker.Server2.Endpoints;

public static class BloodSugarEndpoints
{
    public static IEndpointRouteBuilder MapBloodSugarApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/ns", (IConfiguration config) => config["NIGHTSCOUT_URL"] ?? string.Empty);

        // Blood Sugar API endpoint
        app.MapGet("/api/bloodsugar", async (IBloodSugarService bloodSugarService, CancellationToken cancellationToken) =>
        {
            try
            {
                var bloodSugarStatus = await bloodSugarService.GetBloodSugarStatusAsync(cancellationToken);
                return Results.Ok(bloodSugarStatus);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get blood sugar data: {ex.Message}");
            }
        });

        // Blood Sugar cache management endpoints
        app.MapDelete("/api/bloodsugar/cache", async (IBloodSugarService bloodSugarService) =>
        {
            try
            {
                await bloodSugarService.ClearCacheAsync();
                return Results.Ok(new { message = "Blood sugar cache cleared successfully" });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to clear blood sugar cache: {ex.Message}");
            }
        });

        app.MapPost("/api/bloodsugar/refresh", async (IBloodSugarService bloodSugarService, CancellationToken cancellationToken) =>
        {
            try
            {
                var bloodSugarStatus = await bloodSugarService.RefreshAsync(cancellationToken);
                return Results.Ok(bloodSugarStatus);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to refresh blood sugar data: {ex.Message}");
            }
        });

        return app;
    }
}

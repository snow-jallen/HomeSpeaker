using HomeSpeaker.Server2.Services;
using HomeSpeaker.Server2.Services.BleTemperature;
using HomeSpeaker.Shared.Temperature;

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

        // Bluetooth readings from the speakers (the hub keeps the best per sensor). No auth, like the rest of the
        // household API: a forged reading could at worst skew the climate card or trigger a window alert.
        app.MapPost("/api/temperature/readings", (SensorReading[]? readings, SensorReadingStore store, TimeProvider timeProvider, IConfiguration configuration) =>
        {
            if (!TemperatureSource.IsHub(configuration))
            {
                return Results.Conflict("This speaker isn't the Bluetooth temperature hub (Temperature:Source=bluetooth with no Temperature:HubUrl).");
            }

            if (readings is null || readings.Length > SensorReadingStore.MaxSensors)
            {
                return Results.BadRequest($"Send an array of at most {SensorReadingStore.MaxSensors} readings.");
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            if (readings.Select(r => SensorReadingValidator.Problem(r, now)).FirstOrDefault(p => p is not null) is { } problem)
            {
                return Results.BadRequest(problem);
            }

            foreach (var reading in readings)
            {
                store.Record(reading with { ObservedAt = reading.ObservedAt.ToUniversalTime() });
            }

            return Results.NoContent();
        });

        app.MapGet("/api/temperature/sensors", (SensorReadingStore store) => Results.Ok(store.All));

        return app;
    }
}

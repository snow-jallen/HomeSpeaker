using HomeSpeaker.Server2.Services;
using HomeSpeaker.Shared;
using HomeSpeaker.Shared.WindowMonitoring;

namespace HomeSpeaker.Server2.Endpoints;

public static class PushEndpoints
{
    public static IEndpointRouteBuilder MapPushApi(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/push/devices/register", async (PushNotificationService pushNotificationService, PushDeviceRegistrationRequest request, CancellationToken cancellationToken) =>
        {
            try
            {
                var device = await pushNotificationService.RegisterDeviceAsync(request, cancellationToken);
                return Results.Ok(device);
            }
            catch (ArgumentException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["request"] = [ex.Message]
                });
            }
        });

        app.MapDelete("/api/push/devices/{installationId}", async (PushNotificationService pushNotificationService, string installationId, CancellationToken cancellationToken) =>
        {
            var removed = await pushNotificationService.UnregisterDeviceAsync(installationId, cancellationToken);
            return removed
                ? Results.NoContent()
                : Results.NotFound();
        });

        app.MapGet("/api/homespeaker/push/installations/{installationId}", async (PushNotificationService pushNotificationService, string installationId, CancellationToken cancellationToken) =>
        {
            try
            {
                var status = await pushNotificationService.GetRegistrationStatusAsync(installationId, cancellationToken);
                return Results.Ok(status);
            }
            catch (ArgumentException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["installationId"] = [ex.Message]
                });
            }
        });

        app.MapPut("/api/homespeaker/push/installations/{installationId}", async (PushNotificationService pushNotificationService, string installationId, UpsertPushInstallationRequest request, CancellationToken cancellationToken) =>
        {
            try
            {
                request.InstallationId = installationId;
                var status = await pushNotificationService.UpsertInstallationAsync(request, cancellationToken);
                return Results.Ok(status);
            }
            catch (ArgumentException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["request"] = [ex.Message]
                });
            }
        });

        return app;
    }
}

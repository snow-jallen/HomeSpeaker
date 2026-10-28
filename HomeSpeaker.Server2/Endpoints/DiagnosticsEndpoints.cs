using System.Diagnostics;
using System.Reflection;
using HomeSpeaker.Server2.Telemetry;
using Microsoft.AspNetCore.Authorization;

namespace HomeSpeaker.Server2.Endpoints;

public static class DiagnosticsEndpoints
{
    public static IEndpointRouteBuilder MapDiagnosticsApi(this IEndpointRouteBuilder app)
    {
        // What the server is doing right now and over the last hour: CPU, memory, disk I/O, container memory pressure,
        // library rescans and warning/error counts. Open from the box itself (docker exec homespeaker curl
        // localhost/api/diagnostics); anyone else needs admin, since it includes file names and log messages.
        app.MapGet("/api/diagnostics", async (
            HttpContext context,
            IAuthorizationService authorization,
            Mp3Library library,
            ResourceSampler sampler,
            LogEventCounter logEvents,
            TimeProvider time) =>
        {
            var remote = context.Connection.RemoteIpAddress;
            var isLocal = remote is not null && System.Net.IPAddress.IsLoopback(remote);
            if (!isLocal && !(await authorization.AuthorizeAsync(context.User, "Admin")).Succeeded)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            using var process = Process.GetCurrentProcess();
            var samples = sampler.History;
            return Results.Ok(new
            {
                TimestampUtc = time.GetUtcNow(),
                Version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                StartedUtc = process.StartTime.ToUniversalTime(),
                Uptime = time.GetUtcNow() - process.StartTime.ToUniversalTime(),
                SampleIntervalSeconds = ResourceSampler.Interval.TotalSeconds,
                Latest = samples.Count > 0 ? samples[^1] : null,
                Library = new
                {
                    Songs = library.SongCount,
                    library.IsDirty,
                    library.IsSyncing,
                    RecentSyncs = library.SyncHistory,
                },
                Logs = new
                {
                    WarningsAndErrors = logEvents.Total,
                    ByCategory = logEvents.Counts,
                    Recent = logEvents.Recent,
                },
                Samples = samples,
            });
        });

        return app;
    }
}

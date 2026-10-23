using HomeSpeaker.Server2.Settings;

namespace HomeSpeaker.Server2.Endpoints;

/// <summary>
/// The settings page's data as a REST API, for automation and tests (the page itself saves in-process). All
/// routes need the "Admin" policy, and cross-site browser requests are refused.
/// </summary>
public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsApi(this IEndpointRouteBuilder app)
    {
        // With no PIN set these routes are open, so stop another site's page from posting to them.
        var group = app.MapGroup("/api/settings")
            .RequireAuthorization("Admin")
            .AddEndpointFilter(async (context, next) =>
            {
                var site = context.HttpContext.Request.Headers["Sec-Fetch-Site"].ToString();
                if (string.Equals(site, "cross-site", StringComparison.OrdinalIgnoreCase))
                {
                    return Results.StatusCode(StatusCodes.Status403Forbidden);
                }

                return await next(context);
            });

        group.MapGet("", (IConfiguration configuration, IServiceProvider services) =>
            Results.Ok(SettingsForm.Read(configuration, loadDatabase(services))));

        group.MapPut("", (Dictionary<string, string?> submitted, IConfiguration configuration, IServiceProvider services, ISettingsWriter writer) =>
        {
            var errors = SettingsForm.Validate(submitted);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors.ToDictionary(e => e.Key, e => new[] { e.Value }, StringComparer.OrdinalIgnoreCase));
            }

            var changes = SettingsForm.Changes(SettingsForm.Read(configuration, loadDatabase(services)), submitted);
            if (changes.Count > 0)
            {
                try
                {
                    writer.Save(changes);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
                }
            }

            return Results.Ok(SettingsForm.Read(configuration, loadDatabase(services)));
        });

        group.MapPost("/restart", (HttpContext context, IAppRestarter restarter) =>
        {
            // Stop only once the 202 has gone out, so the caller sees it.
            context.Response.OnCompleted(() =>
            {
                restarter.Restart();
                return Task.CompletedTask;
            });
            return Results.Accepted();
        });

        return app;
    }

    private static IReadOnlyDictionary<string, string> loadDatabase(IServiceProvider services) =>
        services.GetService<SqliteSettingsStore>()?.LoadAll() ?? new Dictionary<string, string>();
}

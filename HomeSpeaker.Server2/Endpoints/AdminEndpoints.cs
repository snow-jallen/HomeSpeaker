using System.Security.Claims;
using HomeSpeaker.Server2.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace HomeSpeaker.Server2.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminApi(this IEndpointRouteBuilder app)
    {
        // Antiforgery stays on for both form posts: the Unlock page renders <AntiforgeryToken/>.
        app.MapPost("/api/admin/unlock", async (
            HttpContext context,
            IConfiguration configuration,
            PinAttemptLimiter limiter,
            [FromForm] string? pin,
            [FromForm] string? returnUrl) =>
        {
            var safeUrl = AdminPin.SafeReturnUrl(returnUrl);
            var encodedUrl = Uri.EscapeDataString(safeUrl);
            var clientKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            if (limiter.IsLocked(clientKey))
            {
                return Results.Redirect($"/unlock?error=locked&returnUrl={encodedUrl}");
            }

            if (pin is null || !AdminPin.Verify(pin, configuration[AdminPin.ConfigKey]))
            {
                limiter.RecordFailure(clientKey);
                return Results.Redirect($"/unlock?error=1&returnUrl={encodedUrl}");
            }

            limiter.Reset(clientKey);
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")], AdminPinHandler.SchemeName);
            await context.SignInAsync(AdminPinHandler.SchemeName, new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });
            return Results.Redirect(safeUrl);
        });

        app.MapPost("/api/admin/lock", async (HttpContext context) =>
        {
            await context.SignOutAsync(AdminPinHandler.SchemeName);
            return Results.Redirect("/");
        });

        app.MapGet("/admin-check", () => Results.NoContent()).RequireAuthorization("Admin");

        return app;
    }
}

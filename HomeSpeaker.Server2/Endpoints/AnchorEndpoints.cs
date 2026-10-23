using HomeSpeaker.Server2.Services;
using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Endpoints;

public static class AnchorEndpoints
{
    public static IEndpointRouteBuilder MapAnchorApi(this IEndpointRouteBuilder app)
    {
        app.MapHub<HomeSpeaker.Server2.Hubs.AnchorHub>("/anchorHub");

        // Anchor API endpoints
        app.MapGet("/api/anchors/definitions", async (AnchorService anchorService) =>
        {
            try
            {
                var definitions = await anchorService.GetActiveAnchorDefinitionsAsync();
                return Results.Ok(definitions);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get anchor definitions: {ex.Message}");
            }
        });

        app.MapPost("/api/anchors/definitions", async (AnchorService anchorService, CreateAnchorDefinitionRequest request) =>
        {
            try
            {
                var definition = await anchorService.CreateAnchorDefinitionAsync(request);
                return Results.Created($"/api/anchors/definitions/{definition.Id}", definition);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to create anchor definition: {ex.Message}");
            }
        });

        app.MapPut("/api/anchors/definitions/{id:int}", async (AnchorService anchorService, int id, CreateAnchorDefinitionRequest request) =>
        {
            try
            {
                var definition = await anchorService.UpdateAnchorDefinitionAsync(id, request);
                return definition != null ? Results.Ok(definition) : Results.NotFound();
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to update anchor definition: {ex.Message}");
            }
        });

        app.MapDelete("/api/anchors/definitions/{id:int}", async (AnchorService anchorService, int id) =>
        {
            try
            {
                var success = await anchorService.DeactivateAnchorDefinitionAsync(id);
                return success ? Results.NoContent() : Results.NotFound();
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to deactivate anchor definition: {ex.Message}");
            }
        });

        app.MapGet("/api/anchors/users/{userId}", async (AnchorService anchorService, string userId) =>
        {
            try
            {
                var userAnchors = await anchorService.GetUserAnchorsAsync(userId);
                return Results.Ok(userAnchors);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get user anchors: {ex.Message}");
            }
        });

        app.MapPost("/api/anchors/users", async (AnchorService anchorService, AssignAnchorToUserRequest request) =>
        {
            try
            {
                var userAnchor = await anchorService.AssignAnchorToUserAsync(request);
                return Results.Created($"/api/anchors/users/{userAnchor.UserId}", userAnchor);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to assign anchor to user: {ex.Message}");
            }
        });

        app.MapDelete("/api/anchors/users/{userId}/{anchorDefinitionId:int}", async (AnchorService anchorService, string userId, int anchorDefinitionId) =>
        {
            try
            {
                var success = await anchorService.RemoveAnchorFromUserAsync(userId, anchorDefinitionId);
                return success ? Results.NoContent() : Results.NotFound();
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to remove anchor from user: {ex.Message}");
            }
        });

        app.MapGet("/api/anchors/daily/{userId}/{date}", async (AnchorService anchorService, string userId, DateOnly date) =>
        {
            try
            {
                var dailyAnchors = await anchorService.GetDailyAnchorsAsync(userId, date);
                return Results.Ok(dailyAnchors);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get daily anchors: {ex.Message}");
            }
        });

        app.MapGet("/api/anchors/daily/{userId}", async (AnchorService anchorService, string userId, DateOnly? startDate, DateOnly? endDate) =>
        {
            try
            {
                var start = startDate ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
                var end = endDate ?? DateOnly.FromDateTime(DateTime.Today);
                var dailyAnchors = await anchorService.GetDailyAnchorsRangeAsync(userId, start, end);
                return Results.Ok(dailyAnchors);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get daily anchors range: {ex.Message}");
            }
        });

        app.MapPost("/api/anchors/daily/create/{userId}/{date}", async (AnchorService anchorService, string userId, DateOnly date) =>
        {
            try
            {
                await anchorService.CreateDailyAnchorsForUserAsync(userId, date);
                return Results.Ok(new { message = "Daily anchors created successfully" });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to create daily anchors: {ex.Message}");
            }
        });

        app.MapPut("/api/anchors/daily/completion", async (AnchorService anchorService, UpdateAnchorCompletionRequest request) =>
        {
            try
            {
                var success = await anchorService.UpdateAnchorCompletionAsync(request);
                return success ? Results.Ok() : Results.NotFound();
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to update anchor completion: {ex.Message}");
            }
        });

        app.MapPost("/api/anchors/daily/ensure-today", async (AnchorService anchorService) =>
        {
            try
            {
                await anchorService.EnsureTodayAnchorsForAllUsersAsync();
                return Results.Ok(new { message = "Today's anchors ensured for all users" });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to ensure today's anchors: {ex.Message}");
            }
        });

        // Get all users who have anchors
        app.MapGet("/api/anchors/users", async (AnchorService anchorService) =>
        {
            try
            {
                var users = await anchorService.GetUsersWithAnchorsAsync();
                return Results.Ok(users);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get users with anchors: {ex.Message}");
            }
        });

        // Get daily anchors for all users in a date range
        app.MapGet("/api/anchors/daily", async (AnchorService anchorService, DateOnly? startDate, DateOnly? endDate) =>
        {
            try
            {
                var allUserAnchors = await anchorService.GetAllUsersDailyAnchorsAsync(startDate, endDate);
                return Results.Ok(allUserAnchors);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to get all users' daily anchors: {ex.Message}");
            }
        });

        return app;
    }
}

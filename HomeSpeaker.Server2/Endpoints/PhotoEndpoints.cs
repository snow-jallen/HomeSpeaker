using HomeSpeaker.Server2.Services.Photos;

namespace HomeSpeaker.Server2.Endpoints;

public static class PhotoEndpoints
{
    public static IEndpointRouteBuilder MapPhotoApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/photos");

        group.MapGet("/next", (PhotoLibrary library) =>
            library.Next() is { } photo ? Results.Ok(photo) : Results.NoContent());

        group.MapGet("/{id}.jpg", (string id, PhotoCacheStore store, HttpContext context) =>
        {
            if (!store.TryGetImagePath(id, out var path))
            {
                return Results.NotFound();
            }

            context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            return TypedResults.PhysicalFile(path, "image/jpeg");
        });

        group.MapPost("/{id}/hide", (string id, PhotoLibrary library) =>
            library.Hide(id) ? Results.NoContent() : Results.NotFound());

        group.MapPost("/sync", (PhotoSyncTrigger trigger) =>
        {
            trigger.Request();
            return Results.Accepted();
        });

        return app;
    }
}

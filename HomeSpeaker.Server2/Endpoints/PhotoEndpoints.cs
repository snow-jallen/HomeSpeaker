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

        group.MapGet("/{id}/video", async (
            string id,
            PhotoLibrary library,
            IImmichClient immich,
            HttpContext context,
            ILogger<PhotoLibrary> logger) =>
        {
            if (!PhotoCacheStore.IsValidAssetId(id) || !library.IsVideo(id))
            {
                return Results.NotFound();
            }

            try
            {
                using var upstream = await immich.OpenVideoAsync(
                    id,
                    context.Request.Headers.Range.ToString() is { Length: > 0 } range ? range : null,
                    context.RequestAborted);
                if (!upstream.IsSuccessStatusCode)
                {
                    logger.LogWarning("Immich returned {StatusCode} for video {AssetId}", (int)upstream.StatusCode, id);
                    return Results.StatusCode(StatusCodes.Status502BadGateway);
                }

                await VideoProxy.CopyAsync(upstream, context.Response, context.RequestAborted);
                return Results.Empty;
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // The browser went away mid-stream (seek, swipe or navigation); nothing to report.
                return Results.Empty;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                logger.LogWarning(ex, "Could not stream video {AssetId} from Immich", id);
                return Results.StatusCode(StatusCodes.Status502BadGateway);
            }
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

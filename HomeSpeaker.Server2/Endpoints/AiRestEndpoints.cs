using HomeSpeaker.Server2.Services;
using HomeSpeaker.Shared;
using Microsoft.AspNetCore.Mvc;

namespace HomeSpeaker.Server2.Endpoints;

public static class AiRestEndpoints
{
    public static RouteGroupBuilder MapAiApi(this WebApplication app)
    {
        var group = app.MapGroup("/api/ai")
            .WithTags("AI");

        group.MapGet("/status", getStatus)
            .WithName("GetAiStatus")
            .WithSummary("Get AI processing status");

        group.MapPost("/process/resume", resumeProcessing)
            .WithName("ResumeAiProcessing")
            .WithSummary("Resume AI processing");

        group.MapGet("/playlists", getPlaylists)
            .WithName("GetAiPlaylists")
            .WithSummary("Get AI genre playlists");

        group.MapGet("/playlists/{genreKey}", getPlaylistByGenre)
            .WithName("GetAiPlaylistByGenre")
            .WithSummary("Get AI playlist by genre");

        group.MapPost("/playlists/{genreKey}/play", playPlaylistByGenre)
            .WithName("PlayAiPlaylistByGenre")
            .WithSummary("Play AI playlist by genre");

        group.MapGet("/similar/{songId:int}", getSimilarSongs)
            .WithName("GetSimilarSongs")
            .WithSummary("Get similar songs");

        group.MapPost("/autoplay/from-current", autoplayFromCurrent)
            .WithName("AutoplayFromCurrent")
            .WithSummary("Start AI autoplay from current track");

        group.MapPost("/feedback", submitFeedback)
            .WithName("SubmitAiFeedback")
            .WithSummary("Submit AI feedback");

        return group;
    }

    private static async Task<IResult> getStatus(
        [FromServices] AiMusicCatalogService catalog,
        CancellationToken cancellationToken)
    {
        var status = await catalog.GetStatusAsync(cancellationToken);
        return Results.Ok(status);
    }

    private static async Task<IResult> resumeProcessing(
        [FromServices] AiProcessingSignal signal,
        [FromServices] AiMusicCatalogService catalog,
        CancellationToken cancellationToken)
    {
        await signal.SignalAsync(cancellationToken);
        var status = await catalog.GetStatusAsync(cancellationToken);
        return Results.Ok(status);
    }

    private static async Task<IResult> getPlaylists(
        [FromServices] AiMusicCatalogService catalog,
        CancellationToken cancellationToken)
    {
        var playlists = await catalog.GetGenreSummariesAsync(cancellationToken);
        return Results.Ok(playlists);
    }

    private static async Task<IResult> getPlaylistByGenre(
        [FromRoute] string genreKey,
        [FromServices] AiMusicCatalogService catalog,
        CancellationToken cancellationToken)
    {
        var playlist = await catalog.GetGenrePlaylistAsync(genreKey, 200, cancellationToken);
        return playlist == null ? Results.NotFound() : Results.Ok(playlist);
    }

    private static async Task<IResult> playPlaylistByGenre(
        [FromRoute] string genreKey,
        [FromServices] AiPlaybackService playback,
        CancellationToken cancellationToken)
    {
        var context = await playback.StartGenreSessionAsync(genreKey, cancellationToken);
        return context == null ? Results.NotFound() : Results.Ok(context);
    }

    private static async Task<IResult> getSimilarSongs(
        [FromRoute] int songId,
        [FromServices] Mp3Library library,
        [FromServices] AiMusicCatalogService catalog,
        CancellationToken cancellationToken)
    {
        var songPath = library.Songs.FirstOrDefault(s => s.SongId == songId)?.Path;
        if (string.IsNullOrWhiteSpace(songPath))
        {
            return Results.NotFound();
        }

        var songs = await catalog.GetSimilarSongsAsync(songPath, 25, cancellationToken);
        return Results.Ok(songs);
    }

    private static async Task<IResult> autoplayFromCurrent(
        [FromServices] AiPlaybackService playback,
        CancellationToken cancellationToken)
    {
        var context = await playback.StartSimilarSessionFromCurrentAsync(cancellationToken);
        return context == null ? Results.NotFound() : Results.Ok(context);
    }

    private static async Task<IResult> submitFeedback(
        [FromBody] AiFeedbackRequest request,
        [FromServices] AiPlaybackService playback,
        CancellationToken cancellationToken)
    {
        await playback.SubmitFeedbackAsync(request, cancellationToken);
        return Results.Ok();
    }
}

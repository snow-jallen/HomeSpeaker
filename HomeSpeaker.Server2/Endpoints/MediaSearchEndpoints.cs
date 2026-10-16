using HomeSpeaker.Server2.Services.MediaSearch;
using HomeSpeaker.Shared;
using Microsoft.AspNetCore.Mvc;

namespace HomeSpeaker.Server2.Endpoints;

/// <summary>"Play ..." by name: what Siri sends when someone says "play King's Singers on Home Speaker".</summary>
public static class MediaSearchEndpoints
{
    public static RouteGroupBuilder MapMediaSearchApi(this WebApplication app)
    {
        var group = app.MapGroup("/api/homespeaker/media")
            .WithTags("HomeSpeaker");

        // GET /api/homespeaker/media/match?query=kings%20singers&kind=Artist&artist=
        group.MapGet("/match", findMedia)
            .WithName("FindMedia")
            .WithSummary("Find the playlist, stream, AI mix, artist, album or song a spoken name means");

        // POST /api/homespeaker/media/play?query=kings%20singers&kind=Artist&artist=
        group.MapPost("/play", playMedia)
            .WithName("PlayMedia")
            .WithSummary("Play the best match for a spoken name in place of what's playing");

        return group;
    }

    private static async Task<IResult> findMedia(
        [FromQuery] string? query,
        [FromQuery] string? kind,
        [FromQuery] string? artist,
        [FromServices] MediaSearchService search,
        CancellationToken cancellationToken)
    {
        var match = await search.FindAsync(new MediaQuery(query ?? "", parseKind(kind), artist), cancellationToken);
        return match == null ? Results.NotFound() : Results.Ok(toDto(match));
    }

    private static async Task<IResult> playMedia(
        [FromQuery] string? query,
        [FromQuery] string? kind,
        [FromQuery] string? artist,
        [FromServices] MediaSearchService search,
        CancellationToken cancellationToken)
    {
        var match = await search.PlayAsync(new MediaQuery(query ?? "", parseKind(kind), artist), cancellationToken);
        return match == null ? Results.NotFound() : Results.Ok(toDto(match));
    }

    /// <summary>A hint, so an unknown or differently cased kind is ignored rather than rejected.</summary>
    private static MediaKind? parseKind(string? kind) =>
        Enum.TryParse<MediaKind>(kind, ignoreCase: true, out var parsed) ? parsed : null;

    private static MediaMatchDto toDto(MediaCandidate match) => new(match.Kind, match.Name, match.Artist);
}

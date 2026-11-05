using HomeSpeaker.Server2.Services.Anchors;
using HomeSpeaker.Shared.Anchors;

namespace HomeSpeaker.Server2.Endpoints;

/// <summary>
/// The anchor API used by the iOS and watch apps and by other HomeSpeakers that use this one as their anchor home.
/// The web pages call <see cref="IAnchorStore"/> directly. No auth, like the rest of the household API.
/// </summary>
public static class AnchorEndpoints
{
    public static IEndpointRouteBuilder MapAnchorApi(this IEndpointRouteBuilder app)
    {
        var anchors = app.MapGroup("/api/anchors").AddEndpointFilter(async (context, next) =>
        {
            try
            {
                return await next(context);
            }
            catch (AnchorSourceUnavailableException)
            {
                return Results.Problem("The anchor home couldn't be reached.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        anchors.MapGet("/board", async (IAnchorStore store, DateOnly? from, DateOnly? to, CancellationToken ct) =>
            Results.Ok(await store.GetBoardAsync(from, to, ct)));

        anchors.MapGet("/people", async (IAnchorStore store, CancellationToken ct) =>
            Results.Ok((await store.GetBoardAsync(cancellationToken: ct)).People));

        anchors.MapGet("/people/{personId}/day", async (IAnchorStore store, string personId, DateOnly? date, CancellationToken ct) =>
            await store.GetDayAsync(personId, date, ct) is { } person ? Results.Ok(person) : Results.NotFound());

        anchors.MapPost("/people", async (IAnchorStore store, AnchorPersonRequest request, CancellationToken ct) =>
            created(await store.CreatePersonAsync(request, ct), p => $"/api/anchors/people/{p.Id}/day"));

        anchors.MapPut("/people/order", async (IAnchorStore store, AnchorOrderRequest request, CancellationToken ct) =>
            toResult(await store.OrderPeopleAsync(request, ct)));

        anchors.MapPut("/people/{personId}", async (IAnchorStore store, string personId, AnchorPersonRequest request, CancellationToken ct) =>
            toResult(await store.UpdatePersonAsync(personId, request, ct)));

        anchors.MapDelete("/people/{personId}", async (IAnchorStore store, string personId, CancellationToken ct) =>
            toResult(await store.ArchivePersonAsync(personId, ct)));

        anchors.MapPost("/people/{personId}/anchors", async (IAnchorStore store, string personId, AnchorRequest request, CancellationToken ct) =>
            created(await store.CreateAnchorAsync(personId, request, ct), a => $"/api/anchors/anchors/{a.Id}"));

        anchors.MapPut("/people/{personId}/anchors/order", async (IAnchorStore store, string personId, AnchorOrderRequest request, CancellationToken ct) =>
            toResult(await store.OrderAnchorsAsync(personId, request, ct)));

        anchors.MapPut("/anchors/{anchorId}", async (IAnchorStore store, string anchorId, AnchorRequest request, CancellationToken ct) =>
            toResult(await store.UpdateAnchorAsync(anchorId, request, ct)));

        anchors.MapPost("/anchors/{anchorId}/retire", async (IAnchorStore store, string anchorId, CancellationToken ct) =>
            toResult(await store.RetireAnchorAsync(anchorId, ct)));

        anchors.MapPost("/anchors/{anchorId}/restore", async (IAnchorStore store, string anchorId, CancellationToken ct) =>
            toResult(await store.RestoreAnchorAsync(anchorId, ct)));

        anchors.MapPut("/anchors/{anchorId}/days/{date}", async (IAnchorStore store, string anchorId, DateOnly date, AnchorLogRequest request, CancellationToken ct) =>
            toResult(await store.SetDoneAsync(anchorId, date, request, ct)));

        return app;
    }

    private static IResult created<T>(AnchorOutcome<T> outcome, Func<T, string> location)
        where T : class =>
        outcome.Succeeded ? Results.Created(location(outcome.Value!), outcome.Value) : toResult(outcome.Result, outcome.Message);

    private static IResult toResult(AnchorResult result, string? message = null) => result switch
    {
        AnchorResult.Ok => Results.NoContent(),
        AnchorResult.NotFound => Results.NotFound(),
        AnchorResult.Invalid => Results.Problem(message ?? "The request isn't valid.", statusCode: StatusCodes.Status400BadRequest),
        AnchorResult.ReadOnly => Results.Problem(message ?? "That anchor belongs to another app and can't be changed here.", statusCode: StatusCodes.Status409Conflict),
        _ => Results.Problem(message ?? "The anchor home couldn't be reached.", statusCode: StatusCodes.Status503ServiceUnavailable),
    };
}

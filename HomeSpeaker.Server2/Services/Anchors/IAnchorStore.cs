using HomeSpeaker.Shared.Anchors;

namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>
/// Everything the anchor pages and the anchor API need. Ids are strings so anchors from other sources can share the
/// same routes. Implementations: this device's database (merged with an external feed), or another HomeSpeaker.
/// </summary>
public interface IAnchorStore
{
    /// <summary>Everyone's anchors with a cell per date in <c>[from, to]</c> (default today only, at most 92 days).</summary>
    Task<AnchorBoardDto> GetBoardAsync(DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default);

    Task<AnchorPersonDto?> GetDayAsync(string personId, DateOnly? date = null, CancellationToken cancellationToken = default);

    Task<AnchorOutcome<AnchorPersonDto>> CreatePersonAsync(AnchorPersonRequest request, CancellationToken cancellationToken = default);

    Task<AnchorResult> UpdatePersonAsync(string personId, AnchorPersonRequest request, CancellationToken cancellationToken = default);

    Task<AnchorResult> OrderPeopleAsync(AnchorOrderRequest request, CancellationToken cancellationToken = default);

    Task<AnchorResult> ArchivePersonAsync(string personId, CancellationToken cancellationToken = default);

    Task<AnchorOutcome<AnchorDto>> CreateAnchorAsync(string personId, AnchorRequest request, CancellationToken cancellationToken = default);

    Task<AnchorResult> UpdateAnchorAsync(string anchorId, AnchorRequest request, CancellationToken cancellationToken = default);

    Task<AnchorResult> OrderAnchorsAsync(string personId, AnchorOrderRequest request, CancellationToken cancellationToken = default);

    Task<AnchorResult> RetireAnchorAsync(string anchorId, CancellationToken cancellationToken = default);

    Task<AnchorResult> RestoreAnchorAsync(string anchorId, CancellationToken cancellationToken = default);

    Task<AnchorResult> SetDoneAsync(string anchorId, DateOnly date, AnchorLogRequest request, CancellationToken cancellationToken = default);
}

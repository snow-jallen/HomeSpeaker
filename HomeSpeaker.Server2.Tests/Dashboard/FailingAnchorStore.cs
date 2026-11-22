using HomeSpeaker.Server2.Services.Anchors;
using HomeSpeaker.Shared.Anchors;
using Microsoft.EntityFrameworkCore;

namespace HomeSpeaker.Server2.Tests.Dashboard;

/// <summary>An anchor store whose saves fail the way SQLite does when the database is locked; reads return an empty board.</summary>
public sealed class FailingAnchorStore : IAnchorStore
{
    private static readonly DateOnly today = new(2026, 11, 21);

    public Task<AnchorBoardDto> GetBoardAsync(DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AnchorBoardDto(today, today, today, []));

    public Task<AnchorResult> SetDoneAsync(string anchorId, DateOnly date, AnchorLogRequest request, CancellationToken cancellationToken = default) =>
        throw new DbUpdateException("database is locked");

    public Task<AnchorPersonDto?> GetDayAsync(string personId, DateOnly? date = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<AnchorOutcome<AnchorPersonDto>> CreatePersonAsync(AnchorPersonRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<AnchorResult> UpdatePersonAsync(string personId, AnchorPersonRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<AnchorResult> OrderPeopleAsync(AnchorOrderRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<AnchorResult> ArchivePersonAsync(string personId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<AnchorOutcome<AnchorDto>> CreateAnchorAsync(string personId, AnchorRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<AnchorResult> UpdateAnchorAsync(string anchorId, AnchorRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<AnchorResult> OrderAnchorsAsync(string personId, AnchorOrderRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<AnchorResult> RetireAnchorAsync(string anchorId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<AnchorResult> RestoreAnchorAsync(string anchorId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

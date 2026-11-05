using HomeSpeaker.Shared.Anchors;

namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>
/// The <see cref="IAnchorStore"/> everything uses. It reads <c>Anchors:Source</c> on every call and forwards to this
/// device's store or the remote one, because a Blazor page keeps one scope for as long as it is open: a settings change
/// has to reach a kiosk that never reloads.
/// </summary>
public sealed class AnchorStoreSelector : IAnchorStore
{
    private readonly IConfiguration configuration;
    private readonly IServiceProvider services;

    public AnchorStoreSelector(IConfiguration configuration, IServiceProvider services)
    {
        this.configuration = configuration;
        this.services = services;
    }

    private IAnchorStore Current => AnchorSourceSettings.IsRemote(configuration)
        ? services.GetRequiredService<RemoteAnchorStore>()
        : services.GetRequiredService<LocalAnchorStore>();

    public Task<AnchorBoardDto> GetBoardAsync(DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default) =>
        Current.GetBoardAsync(from, to, cancellationToken);

    public Task<AnchorPersonDto?> GetDayAsync(string personId, DateOnly? date = null, CancellationToken cancellationToken = default) =>
        Current.GetDayAsync(personId, date, cancellationToken);

    public Task<AnchorOutcome<AnchorPersonDto>> CreatePersonAsync(AnchorPersonRequest request, CancellationToken cancellationToken = default) =>
        Current.CreatePersonAsync(request, cancellationToken);

    public Task<AnchorResult> UpdatePersonAsync(string personId, AnchorPersonRequest request, CancellationToken cancellationToken = default) =>
        Current.UpdatePersonAsync(personId, request, cancellationToken);

    public Task<AnchorResult> OrderPeopleAsync(AnchorOrderRequest request, CancellationToken cancellationToken = default) =>
        Current.OrderPeopleAsync(request, cancellationToken);

    public Task<AnchorResult> ArchivePersonAsync(string personId, CancellationToken cancellationToken = default) =>
        Current.ArchivePersonAsync(personId, cancellationToken);

    public Task<AnchorOutcome<AnchorDto>> CreateAnchorAsync(string personId, AnchorRequest request, CancellationToken cancellationToken = default) =>
        Current.CreateAnchorAsync(personId, request, cancellationToken);

    public Task<AnchorResult> UpdateAnchorAsync(string anchorId, AnchorRequest request, CancellationToken cancellationToken = default) =>
        Current.UpdateAnchorAsync(anchorId, request, cancellationToken);

    public Task<AnchorResult> OrderAnchorsAsync(string personId, AnchorOrderRequest request, CancellationToken cancellationToken = default) =>
        Current.OrderAnchorsAsync(personId, request, cancellationToken);

    public Task<AnchorResult> RetireAnchorAsync(string anchorId, CancellationToken cancellationToken = default) =>
        Current.RetireAnchorAsync(anchorId, cancellationToken);

    public Task<AnchorResult> RestoreAnchorAsync(string anchorId, CancellationToken cancellationToken = default) =>
        Current.RestoreAnchorAsync(anchorId, cancellationToken);

    public Task<AnchorResult> SetDoneAsync(string anchorId, DateOnly date, AnchorLogRequest request, CancellationToken cancellationToken = default) =>
        Current.SetDoneAsync(anchorId, date, request, cancellationToken);
}

namespace HomeSpeaker.Server2.Services.Home;

/// <summary>
/// Slot for a home-automation provider (e.g. Home Assistant). The UI renders whatever tiles the
/// registered implementation returns; with NullHomeControl the Home row is hidden entirely.
/// </summary>
public interface IHomeControl
{
    bool IsAvailable { get; }

    event Action? TilesChanged;

    Task<IReadOnlyList<HomeTile>> GetTilesAsync(CancellationToken cancellationToken = default);

    Task InvokeAsync(string tileId, HomeAction action, CancellationToken cancellationToken = default);
}

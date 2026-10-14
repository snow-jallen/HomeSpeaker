using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Services;

public interface IPlayerStatusProvider
{
    /// <summary>Reads the player's current status. Does not log, so it is safe to poll.</summary>
    Task<PlayerStatusDto> GetAsync(CancellationToken cancellationToken);
}

using HomeSpeaker.Shared.BloodSugar;

namespace HomeSpeaker.Server2.Services;

public interface IBloodSugarService
{
    Task<BloodSugarStatus> GetBloodSugarStatusAsync(CancellationToken cancellationToken = default);
    Task<bool> ClearCacheAsync(CancellationToken cancellationToken = default);
    Task<BloodSugarStatus> RefreshAsync(CancellationToken cancellationToken = default);
}

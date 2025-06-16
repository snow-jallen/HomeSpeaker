using HomeSpeaker.Shared.BloodSugar;

namespace HomeSpeaker.WebAssembly.Services;

public interface IBloodSugarService
{
    Task<BloodSugarStatus> GetBloodSugarStatusAsync(CancellationToken cancellationToken = default);
    Task<bool> ClearCacheAsync(CancellationToken cancellationToken = default);
    Task<BloodSugarStatus> RefreshAsync(CancellationToken cancellationToken = default);
}

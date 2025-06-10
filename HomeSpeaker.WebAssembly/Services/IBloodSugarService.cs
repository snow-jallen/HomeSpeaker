using HomeSpeaker.Shared.BloodSugar;

namespace HomeSpeaker.WebAssembly.Services;

public interface IBloodSugarService
{
    Task<BloodSugarStatus> GetBloodSugarStatusAsync(CancellationToken cancellationToken = default);
}

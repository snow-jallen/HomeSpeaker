using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.WebAssembly.Services;

public interface ITemperatureService
{
    Task<TemperatureStatus> GetTemperatureStatusAsync(CancellationToken cancellationToken = default);
}

using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.Server2.Services;

public interface ITemperatureService
{
    Task<TemperatureStatus> GetTemperatureStatusAsync(CancellationToken cancellationToken = default);
    Task<SensorDiagnosticsReport> GetSensorDiagnosticsAsync(CancellationToken cancellationToken = default);
    Task<bool> ClearCacheAsync(CancellationToken cancellationToken = default);
    Task<TemperatureStatus> RefreshAsync(CancellationToken cancellationToken = default);
}

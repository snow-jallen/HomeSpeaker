using HomeSpeaker.WebAssembly.Models.Temperature;

namespace HomeSpeaker.WebAssembly.Services;

public interface ITemperatureService
{
    Task<List<Device>> GetDevicesAsync(CancellationToken cancellationToken = default);
    Task<double> GetDeviceTemperatureAsync(Device device, CancellationToken cancellationToken = default);
    Task<TemperatureStatus> GetTemperatureStatusAsync(CancellationToken cancellationToken = default);
}

using HomeSpeaker.Server2.Services.BleTemperature;
using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.Server2.Services;

/// <summary>
/// The <see cref="ITemperatureService"/> everything uses. It reads <c>Temperature:Source</c> on every call and
/// forwards to the Govee cloud or the Bluetooth service, so a settings change reaches a kiosk page that never reloads.
/// </summary>
public sealed class TemperatureServiceSelector : ITemperatureService
{
    private readonly IConfiguration configuration;
    private readonly IServiceProvider services;

    public TemperatureServiceSelector(IConfiguration configuration, IServiceProvider services)
    {
        this.configuration = configuration;
        this.services = services;
    }

    private ITemperatureService Current => TemperatureSource.IsBluetooth(configuration)
        ? services.GetRequiredService<BluetoothTemperatureService>()
        : services.GetRequiredService<TemperatureService>();

    public Task<List<Device>> GetDevicesAsync(CancellationToken cancellationToken = default) => Current.GetDevicesAsync(cancellationToken);

    public Task<TemperatureStatus> GetTemperatureStatusAsync(CancellationToken cancellationToken = default) => Current.GetTemperatureStatusAsync(cancellationToken);

    public Task<SensorDiagnosticsReport> GetSensorDiagnosticsAsync(CancellationToken cancellationToken = default) => Current.GetSensorDiagnosticsAsync(cancellationToken);

    public Task<bool> ClearCacheAsync(CancellationToken cancellationToken = default) => Current.ClearCacheAsync(cancellationToken);

    public Task<TemperatureStatus> RefreshAsync(CancellationToken cancellationToken = default) => Current.RefreshAsync(cancellationToken);
}

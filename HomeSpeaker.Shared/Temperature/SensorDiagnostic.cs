namespace HomeSpeaker.Shared.Temperature;

#nullable enable

/// <summary>What one matched sensor reports, for checking on a live Pi whether (for example) battery is available.</summary>
public sealed class SensorDiagnostic
{
    public required string Room { get; init; }
    public required string DeviceName { get; init; }
    public required string Sku { get; init; }

    /// <summary>False when the state request failed, in which case <see cref="CapabilityInstances"/> is empty.</summary>
    public bool StateAvailable { get; init; }

    /// <summary>The capability instance names in the device's latest state, e.g. sensorTemperature, sensorHumidity, online.</summary>
    public IReadOnlyList<string> CapabilityInstances { get; init; } = [];

    public double? BatteryPercent { get; init; }
}

namespace HomeSpeaker.Shared.Temperature;

#nullable enable

/// <summary>The result of GET /api/temperature/capabilities.</summary>
public sealed class SensorDiagnosticsReport
{
    /// <summary>One entry per room that matched a device, in room order.</summary>
    public List<SensorDiagnostic> Sensors { get; init; } = [];

    /// <summary>
    /// Devices on the account that no room claimed (name and SKU only), so a room pattern that does not match the
    /// real device name can be spotted and fixed.
    /// </summary>
    public List<UnmatchedDevice> UnmatchedDevices { get; init; } = [];
}

public sealed class UnmatchedDevice
{
    public required string Name { get; init; }
    public required string Sku { get; init; }
}

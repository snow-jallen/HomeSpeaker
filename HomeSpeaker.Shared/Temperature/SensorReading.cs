namespace HomeSpeaker.Shared.Temperature;

#nullable enable

/// <summary>
/// One Bluetooth temperature sensor's latest advertisement as heard by one speaker. Speakers POST these to the hub
/// (<c>POST /api/temperature/readings</c>), which keeps the best one per sensor.
/// </summary>
public sealed record SensorReading
{
    /// <summary>The sensor's Bluetooth address, upper case with colons (C9:38:30:34:17:76).</summary>
    public required string Mac { get; init; }

    /// <summary>The advertised name (GVH5100_1776), or the address when the sensor hasn't sent one.</summary>
    public required string Name { get; init; }

    public double TemperatureC { get; init; }

    public double Humidity { get; init; }

    public int BatteryPercent { get; init; }

    /// <summary>Signal strength in dBm at the speaker that heard it; used to pick between speakers.</summary>
    public int Rssi { get; init; }

    /// <summary>When the advertisement arrived (UTC).</summary>
    public DateTime ObservedAt { get; init; }

    /// <summary>Which speaker heard it.</summary>
    public string Reporter { get; init; } = "";
}

namespace HomeSpeaker.Shared.Temperature;

#nullable enable

public sealed class TemperatureStatus
{
    public double? OutsideTemperature { get; set; }
    public string? OutsideRoomName { get; set; }

    /// <summary>The room compared against outside for window advice; null (with <see cref="IndoorTemperature"/>) when advice is off or unavailable.</summary>
    public string? IndoorRoomName { get; set; }
    public double? IndoorTemperature { get; set; }

    /// <summary>
    /// Every configured room (the outside room included), in configuration order, with its battery level where reported.
    /// </summary>
    public List<RoomReading> Rooms { get; set; } = [];

    public DateTime ReadingTakenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastCachedAt { get; set; } = DateTime.UtcNow;
    public bool ShouldWindowsBeClosed { get; set; }
    public double TemperatureDifference { get; set; }
    public bool IsWithinThreshold { get; set; }
    public double Threshold { get; set; }
}

using System;

namespace HomeSpeaker.Shared.Temperature;

#nullable enable

public sealed class TemperatureStatus
{
    public double? OutsideTemperature { get; set; }
    public double? YoungerGirlsRoomTemperature { get; set; }
    public double? OlderGirlsRoomTemperature { get; set; }
    public double? BoysRoomTemperature { get; set; }
    public double? MomAndDadsRoomTemperature { get; set; }
    public double? GreenhouseTemperature { get; set; }
    public DateTime ReadingTakenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastCachedAt { get; set; } = DateTime.UtcNow;
    public bool ShouldWindowsBeClosed { get; set; }
    public double TemperatureDifference { get; set; }
    public bool IsWithinThreshold { get; set; }
    public double Threshold { get; set; }
}

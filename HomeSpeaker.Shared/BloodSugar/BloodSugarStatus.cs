using System;

namespace HomeSpeaker.Shared.BloodSugar;

#nullable enable

public sealed class BloodSugarStatus
{
    public BloodSugarReading? CurrentReading { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.Now;
    public bool IsStale { get; set; }
    public TimeSpan TimeSinceLastReading { get; set; }
    
    public string StatusColor => CurrentReading?.Sgv switch
    {
        null => "#6c757d", // Gray for no data
        var sgv when sgv < 70 => "#dc3545", // Red for low
        var sgv when sgv < 80 => "#fd7e14", // Orange for borderline low
        var sgv when sgv <= 180 => "#28a745", // Green for in range
        var sgv when sgv <= 250 => "#fd7e14", // Orange for high
        _ => "#dc3545" // Red for very high
    };
    
    public string StatusText => CurrentReading?.Sgv switch
    {
        null => "No Data",
        var sgv when sgv < 70 => "Low",
        var sgv when sgv < 80 => "Below Target",
        var sgv when sgv <= 180 => "In Range",
        var sgv when sgv <= 250 => "Above Target",
        _ => "High"
    };
}

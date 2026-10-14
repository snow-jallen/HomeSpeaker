namespace HomeSpeaker.Shared.BloodSugar;

#nullable enable

public sealed class BloodSugarStatus
{
    /// <summary>The newest reading; always the same reading as the first of <see cref="RecentReadings"/> when that is non-empty.</summary>
    public BloodSugarReading? CurrentReading { get; set; }

    /// <summary>
    /// Readings from roughly the last three hours, NEWEST FIRST (the order NightScout returns them).
    /// Fetched in the same request as <see cref="CurrentReading"/>.
    /// </summary>
    public List<BloodSugarReading> RecentReadings { get; set; } = [];

    /// <summary>A previous reading further back than this is too old to say what the glucose "just did".</summary>
    public static readonly TimeSpan DeltaMaxGap = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Change since the reading before <see cref="CurrentReading"/> (positive = rising), or null when there
    /// is no earlier reading within <see cref="DeltaMaxGap"/>.
    /// </summary>
    public double? Delta
    {
        get
        {
            if (this.CurrentReading is not { } current)
            {
                return null;
            }

            var previous = this.RecentReadings
                .Where(r => r.Date < current.Date)
                .OrderByDescending(r => r.Date)
                .FirstOrDefault();

            return previous is not null && current.Date - previous.Date <= DeltaMaxGap
                ? current.Sgv - previous.Sgv
                : null;
        }
    }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public bool IsStale { get; set; }
    public TimeSpan TimeSinceLastReading { get; set; }

    public string StatusColor => this.CurrentReading?.Sgv switch
    {
        null => "#6c757d", // Gray for no data
        var sgv when sgv < 70 => "#dc3545", // Red for low
        var sgv when sgv < 80 => "#fd7e14", // Orange for borderline low
        var sgv when sgv <= 180 => "#28a745", // Green for in range
        var sgv when sgv <= 250 => "#fd7e14", // Orange for high
        _ => "#dc3545" // Red for very high
    };

    public string StatusText => this.CurrentReading?.Sgv switch
    {
        null => "No Data",
        var sgv when sgv < 70 => "Low",
        var sgv when sgv < 80 => "Below Target",
        var sgv when sgv <= 180 => "In Range",
        var sgv when sgv <= 250 => "Above Target",
        _ => "High"
    };
}

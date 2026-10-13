namespace HomeSpeaker.Server2.Services.Dashboard;

/// <summary>Same thresholds as BloodSugarStatus.StatusColor: red &lt;70 or &gt;250, orange 70-79 and 181-250.</summary>
public static class GlucoseToneMap
{
    public static GlucoseTone From(double? sgv) => sgv switch
    {
        null => GlucoseTone.Unknown,
        < 70 => GlucoseTone.Urgent,
        < 80 => GlucoseTone.Caution,
        <= 180 => GlucoseTone.Normal,
        <= 250 => GlucoseTone.Caution,
        _ => GlucoseTone.Urgent,
    };
}

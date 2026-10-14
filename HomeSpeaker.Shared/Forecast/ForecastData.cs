namespace HomeSpeaker.Shared.Forecast;

#nullable enable

public sealed class ForecastData
{
    public DateTime DateTime { get; set; }
    public double? Temperature { get; set; }
    public string? Conditions { get; set; }
    public string? IconUrl { get; set; }
    public double? PrecipitationChance { get; set; }

    /// <summary>The WMO weather code for this hour (hourly entries only), used to choose an icon.</summary>
    public int? WeatherCode { get; set; }

    /// <summary>True in daylight, false at night (hourly entries only), so the icon can be a sun or a moon.</summary>
    public bool? IsDay { get; set; }
}

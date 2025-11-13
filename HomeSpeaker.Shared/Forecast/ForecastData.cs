using System;

namespace HomeSpeaker.Shared.Forecast;

#nullable enable

public sealed class ForecastData
{
    public DateTime DateTime { get; set; }
    public double? Temperature { get; set; }
    public string? Conditions { get; set; }
    public string? IconUrl { get; set; }
    public double? PrecipitationChance { get; set; }
}

using System;

namespace HomeSpeaker.Shared.BloodSugar;

#nullable enable

public sealed class BloodSugarReading
{
    public double Sgv { get; set; }
    //public DateTime Date { get; set; }
    public DateTime Date => DateString;
    public string Direction { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime DateString { get; set; }

    public string DirectionIcon => Direction switch
    {
        "Flat" => "→",
        "SingleUp" => "↗",
        "DoubleUp" => "⬆",
        "SingleDown" => "↘",
        "DoubleDown" => "⬇",
        "FortyFiveUp" => "↗",
        "FortyFiveDown" => "↘",
        _ => "?"
    };

    public string DirectionDescription => Direction switch
    {
        "Flat" => "Stable",
        "SingleUp" => "Rising slowly",
        "DoubleUp" => "Rising rapidly",
        "SingleDown" => "Falling slowly",
        "DoubleDown" => "Falling rapidly",
        "FortyFiveUp" => "Rising",
        "FortyFiveDown" => "Falling",
        _ => "Unknown"
    };
}

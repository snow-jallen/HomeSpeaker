namespace HomeSpeaker.Server2.Services.Dashboard;

/// <summary>Which icon an hour of forecast gets. Clear and PartlyCloudy have night variants (see <see cref="HourlyColumn.IsDay"/>).</summary>
public enum WeatherIcon
{
    Clear,
    PartlyCloudy,
    Cloudy,
    Fog,
    Drizzle,
    Rain,
    Snow,
    Thunderstorm,
}

/// <summary>Maps WMO weather codes (as Open-Meteo reports them) to an icon category.</summary>
public static class WeatherIconMap
{
    public static WeatherIcon From(int? weatherCode) => weatherCode switch
    {
        0 or 1 => WeatherIcon.Clear,
        2 => WeatherIcon.PartlyCloudy,
        3 => WeatherIcon.Cloudy,
        45 or 48 => WeatherIcon.Fog,
        51 or 53 or 55 or 56 or 57 => WeatherIcon.Drizzle,
        61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => WeatherIcon.Rain,
        71 or 73 or 75 or 77 or 85 or 86 => WeatherIcon.Snow,
        95 or 96 or 99 => WeatherIcon.Thunderstorm,
        _ => WeatherIcon.Cloudy,
    };
}

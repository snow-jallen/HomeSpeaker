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

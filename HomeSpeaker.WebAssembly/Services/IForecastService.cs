using HomeSpeaker.Shared.Forecast;

namespace HomeSpeaker.WebAssembly.Services;

public interface IForecastService
{
    Task<ForecastStatus> GetForecastStatusAsync();
    Task<ForecastStatus> RefreshAsync();
    Task<bool> ClearCacheAsync();
}

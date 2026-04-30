using HomeSpeaker.Shared.Forecast;

namespace HomeSpeaker.Server2.Services;

public interface IForecastService
{
    Task<ForecastStatus> GetForecastStatusAsync(CancellationToken cancellationToken = default);
    Task<bool> ClearCacheAsync(CancellationToken cancellationToken = default);
    Task<ForecastStatus> RefreshAsync(CancellationToken cancellationToken = default);
}

namespace HomeSpeaker.Server;

public class AirPlayHostedService : IHostedService
{
    private readonly IAirPlayService _airPlayService;

    public AirPlayHostedService(IAirPlayService airPlayService)
    {
        _airPlayService = airPlayService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _airPlayService.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _airPlayService.StopAsync(cancellationToken);
    }
}

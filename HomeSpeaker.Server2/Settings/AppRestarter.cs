namespace HomeSpeaker.Server2.Settings;

public sealed class AppRestarter(IHostApplicationLifetime lifetime, ILogger<AppRestarter> logger) : IAppRestarter
{
    public void Restart()
    {
        logger.LogWarning("Restart requested from settings; stopping the application");
        lifetime.StopApplication();
    }
}

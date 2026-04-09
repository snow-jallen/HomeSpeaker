namespace HomeSpeaker.Server2.Services;

public class AirPlayReceiverService : BackgroundService
{
    private readonly ILogger<AirPlayReceiverService> logger;
    private readonly IMusicPlayer musicPlayer;

    private const string MetadataPipePath = "/tmp/airplay-shared/metadata";
    private const string AirPlayStatePath = "/tmp/airplay-shared/state";
    private bool airplayActive;

    public AirPlayReceiverService(ILogger<AirPlayReceiverService> logger, IMusicPlayer musicPlayer)
    {
        this.logger = logger;
        this.musicPlayer = musicPlayer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("AirPlay Receiver Service starting...");

        var eventsTask = Task.Run(() => monitorAirPlayEvents(stoppingToken), stoppingToken);
        var metadataTask = Task.Run(() => monitorAirPlayMetadata(stoppingToken), stoppingToken);

        await Task.WhenAll(eventsTask, metadataTask).ConfigureAwait(false);
    }

    private async Task monitorAirPlayEvents(CancellationToken cancellationToken)
    {
        // Monitor the shared state file written by ShairportSync scripts
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (File.Exists(AirPlayStatePath))
                {
                    var stateContent = await File.ReadAllTextAsync(AirPlayStatePath, cancellationToken);
                    var currentlyActive = stateContent.Trim().Equals("ACTIVE", StringComparison.OrdinalIgnoreCase);

                    if (currentlyActive && !airplayActive)
                    {
                        logger.LogInformation("AirPlay session started - pausing local playback");
                        musicPlayer.Stop(); // Pause local music when AirPlay starts
                        airplayActive = true;
                    }
                    else if (!currentlyActive && airplayActive)
                    {
                        logger.LogInformation("AirPlay session ended - can resume local playback");
                        airplayActive = false;
                        // Optionally auto-resume: musicPlayer.ResumePlay();
                    }
                }

                await Task.Delay(1000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error monitoring AirPlay state file");
                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    private async Task monitorAirPlayMetadata(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (File.Exists(MetadataPipePath))
                {
                    using var reader = new StreamReader(MetadataPipePath);
                    var metadata = await reader.ReadToEndAsync(cancellationToken);

                    if (!string.IsNullOrEmpty(metadata))
                    {
                        logger.LogInformation("AirPlay metadata: {Metadata}", metadata);
                        // Parse metadata and update UI if needed
                        // Could send events through your existing SendEvent mechanism
                    }
                }

                await Task.Delay(500, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error reading AirPlay metadata");
                await Task.Delay(2000, cancellationToken);
            }
        }
    }
}
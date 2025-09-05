using System.IO.Pipes;
using HomeSpeaker.Server2;
using System.Diagnostics;

namespace HomeSpeaker.Server2.Services;

public class AirPlayReceiverService : BackgroundService
{
    private readonly ILogger<AirPlayReceiverService> _logger;
    private readonly IMusicPlayer _musicPlayer;

    private const string MetadataPipePath = "/tmp/airplay-shared/metadata";
    private const string AirPlayStatePath = "/tmp/airplay-shared/state";
    private const string AirPlayLogPath = "/tmp/airplay-shared/log";
    private bool _airplayActive;

    public AirPlayReceiverService(ILogger<AirPlayReceiverService> logger, IMusicPlayer musicPlayer)
    {
        _logger = logger;
        _musicPlayer = musicPlayer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AirPlay Receiver Service starting...");

        // Monitor for AirPlay session events via shared state file
        _ = Task.Run(() => MonitorAirPlayEvents(stoppingToken), stoppingToken);
        
        // Monitor for metadata (song info, artwork, etc.)
        _ = Task.Run(() => MonitorAirPlayMetadata(stoppingToken), stoppingToken);

        // Keep service running
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task MonitorAirPlayEvents(CancellationToken cancellationToken)
    {
        // Monitor the shared state file written by ShairportSync scripts
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (File.Exists(AirPlayStatePath))
                {
                    var stateContent = await File.ReadAllTextAsync(AirPlayStatePath, cancellationToken);
                    bool currentlyActive = stateContent.Trim().Equals("ACTIVE", StringComparison.OrdinalIgnoreCase);
                    
                    if (currentlyActive && !_airplayActive)
                    {
                        _logger.LogInformation("AirPlay session started - pausing local playback");
                        _musicPlayer.Stop(); // Pause local music when AirPlay starts
                        _airplayActive = true;
                    }
                    else if (!currentlyActive && _airplayActive)
                    {
                        _logger.LogInformation("AirPlay session ended - can resume local playback");
                        _airplayActive = false;
                        // Optionally auto-resume: musicPlayer.ResumePlay();
                    }
                }
                
                await Task.Delay(1000, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring AirPlay state file");
                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    private async Task MonitorAirPlayMetadata(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (File.Exists(MetadataPipePath))
                {
                    using var reader = new StreamReader(MetadataPipePath);
                    var metadata = await reader.ReadToEndAsync();
                    
                    if (!string.IsNullOrEmpty(metadata))
                    {
                        _logger.LogInformation("AirPlay metadata: {metadata}", metadata);
                        // Parse metadata and update UI if needed
                        // Could send events through your existing SendEvent mechanism
                    }
                }
                
                await Task.Delay(500, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading AirPlay metadata");
                await Task.Delay(2000, cancellationToken);
            }
        }
    }
}

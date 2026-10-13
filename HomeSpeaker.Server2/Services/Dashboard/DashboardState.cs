using HomeSpeaker.Server2.Models;
using HomeSpeaker.Server2.Services.Photos;
using HomeSpeaker.Shared.BloodSugar;
using HomeSpeaker.Shared.Forecast;
using HomeSpeaker.Shared.Temperature;
using SharedPlayerStatus = HomeSpeaker.Shared.PlayerStatus;

namespace HomeSpeaker.Server2.Services.Dashboard;

/// <summary>
/// One per Blazor circuit: the live data behind the ambient overlay, the night clock and the Home page,
/// so they share one set of timers instead of each polling on its own.
/// </summary>
public sealed class DashboardState : IDisposable
{
    private static readonly TimeSpan statusInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan healthInterval = TimeSpan.FromSeconds(60);

    private readonly HomeSpeakerService homeSpeaker;
    private readonly PlayerStateService playerState;
    private readonly IBloodSugarService bloodSugarService;
    private readonly ITemperatureService temperatureService;
    private readonly IForecastService forecastService;
    private readonly PhotoLibrary photoLibrary;
    private readonly ILogger<DashboardState> logger;
    private readonly SemaphoreSlim statusGate = new(1, 1);
    private readonly Lock lifecycleGate = new();
    private Timer? statusTimer;
    private Timer? healthTimer;
    private bool started;
    private volatile bool disposed;

    public DashboardState(
        HomeSpeakerService homeSpeaker,
        PlayerStateService playerState,
        IBloodSugarService bloodSugarService,
        ITemperatureService temperatureService,
        IForecastService forecastService,
        IConfiguration configuration,
        PhotoLibrary photoLibrary,
        ILogger<DashboardState> logger)
    {
        this.homeSpeaker = homeSpeaker;
        this.playerState = playerState;
        this.bloodSugarService = bloodSugarService;
        this.temperatureService = temperatureService;
        this.forecastService = forecastService;
        this.photoLibrary = photoLibrary;
        this.logger = logger;
        BloodSugarEnabled = FeatureFlags.BloodSugarEnabled(configuration);
        TemperatureEnabled = FeatureFlags.TemperatureEnabled(configuration);
    }

    public event Action? Changed;

    public SharedPlayerStatus? Status => playerState.Status;
    public bool RepeatMode => playerState.RepeatMode;
    public int Volume => playerState.Volume;
    public SongViewModel? UpNext { get; private set; }

    /// <summary>The playing station's uploaded art, or null when nothing matches (the UI then generates a tile).</summary>
    public string? StationImageUrl { get; private set; }
    public BloodSugarStatus? BloodSugar { get; private set; }
    public TemperatureStatus? Temperature { get; private set; }
    public ForecastStatus? Forecast { get; private set; }
    public bool BloodSugarEnabled { get; }
    public bool TemperatureEnabled { get; }
    public bool PhotosEnabled => photoLibrary.Count > 0;
    public GlucoseTone GlucoseTone => GlucoseToneMap.From(BloodSugar?.CurrentReading?.Sgv);

    public async Task StartAsync()
    {
        lock (lifecycleGate)
        {
            if (started || disposed)
            {
                return;
            }

            started = true;
            homeSpeaker.StatusChanged += onPlayerEvent;
            homeSpeaker.QueueChanged += onQueueChanged;
            playerState.StateChanged += onPlayerStateChanged;
        }

        await Task.WhenAll(RefreshStatusAsync(), refreshHealthAsync());

        // Dispose may have run during the awaits above; it takes the same lock, so either it already
        // ran (we skip creating timers) or it will run after the timers exist and dispose them.
        lock (lifecycleGate)
        {
            if (disposed)
            {
                return;
            }

            statusTimer = new Timer(state => _ = RefreshStatusAsync(), null, statusInterval, statusInterval);
            healthTimer = new Timer(state => _ = refreshHealthAsync(), null, healthInterval, healthInterval);
        }
    }

    public async Task RefreshStatusAsync()
    {
        try
        {
            if (disposed || !await statusGate.WaitAsync(0))
            {
                return;
            }
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            var status = await homeSpeaker.GetStatusAsync();
            var repeat = await homeSpeaker.GetRepeatModeAsync();
            UpNext = (await homeSpeaker.GetPlayQueueAsync()).FirstOrDefault();
            StationImageUrl = status.IsStream ? await homeSpeaker.GetStationImageUrlAsync(status.StreamName) : null;
            playerState.UpdateStatus(status);
            playerState.UpdateRepeatMode(repeat);
            playerState.UpdateVolume(status.Volume);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Dashboard status refresh failed");
        }
        finally
        {
            if (!disposed)
            {
                try
                {
                    statusGate.Release();
                }
                catch (ObjectDisposedException)
                {
                    // Disposed between the check and the release.
                }
            }
        }
    }

    public void Dispose()
    {
        lock (lifecycleGate)
        {
            disposed = true;
            homeSpeaker.StatusChanged -= onPlayerEvent;
            homeSpeaker.QueueChanged -= onQueueChanged;
            playerState.StateChanged -= onPlayerStateChanged;
            statusTimer?.Dispose();
            healthTimer?.Dispose();
            statusGate.Dispose();
        }
    }

    private async Task refreshHealthAsync()
    {
        if (disposed)
        {
            return;
        }

        if (BloodSugarEnabled)
        {
            BloodSugar = await tryGetAsync(() => bloodSugarService.GetBloodSugarStatusAsync(), "blood sugar") ?? BloodSugar;
        }

        if (TemperatureEnabled)
        {
            Temperature = await tryGetAsync(() => temperatureService.GetTemperatureStatusAsync(), "temperature") ?? Temperature;
        }

        Forecast = await tryGetAsync(() => forecastService.GetForecastStatusAsync(), "forecast") ?? Forecast;

        if (!disposed)
        {
            try
            {
                Changed?.Invoke();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Dashboard Changed subscriber threw after health refresh");
            }
        }
    }

    private async Task<T?> tryGetAsync<T>(Func<Task<T>> fetch, string what)
        where T : class
    {
        try
        {
            return await fetch();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Dashboard {What} refresh failed", what);
            return null;
        }
    }

    private void onPlayerEvent(string message) => _ = RefreshStatusAsync();

    private void onQueueChanged(object? sender, EventArgs e) => _ = RefreshStatusAsync();

    private void onPlayerStateChanged()
    {
        if (!disposed)
        {
            Changed?.Invoke();
        }
    }
}

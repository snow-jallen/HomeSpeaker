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
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<DashboardState> logger;
    private readonly SemaphoreSlim statusGate = new(1, 1);
    private readonly Lock lifecycleGate = new();
    private Timer? statusTimer;
    private Timer? healthTimer;
    private bool started;
    private volatile bool disposed;
    private volatile StationImage? stationImage;

    private sealed record StationImage(string? Key, string? Url);

    public DashboardState(
        HomeSpeakerService homeSpeaker,
        PlayerStateService playerState,
        IBloodSugarService bloodSugarService,
        ITemperatureService temperatureService,
        IForecastService forecastService,
        IConfiguration configuration,
        PhotoLibrary photoLibrary,
        IServiceScopeFactory scopeFactory,
        ILogger<DashboardState> logger)
    {
        this.homeSpeaker = homeSpeaker;
        this.playerState = playerState;
        this.bloodSugarService = bloodSugarService;
        this.temperatureService = temperatureService;
        this.forecastService = forecastService;
        this.photoLibrary = photoLibrary;
        this.scopeFactory = scopeFactory;
        this.logger = logger;
        BloodSugarEnabled = FeatureFlags.BloodSugarEnabled(configuration);
        TemperatureEnabled = FeatureFlags.TemperatureEnabled(configuration);
        ForecastEnabled = FeatureFlags.ForecastEnabled(configuration);
        AnchorsEnabled = FeatureFlags.AnchorsEnabled(configuration);
        TemperatureUnit = TemperatureDisplay.UnitFrom(configuration);
    }

    /// <summary>The unit temperatures are shown in (readings themselves are Fahrenheit).</summary>
    public TemperatureUnit TemperatureUnit { get; }

    public event Action? Changed;

    public SharedPlayerStatus? Status => playerState.Status;
    public bool RepeatMode => playerState.RepeatMode;
    public int Volume => playerState.Volume;
    public SongViewModel? UpNext { get; private set; }

    /// <summary>
    /// The playing station's uploaded art, or null when nothing matches (the UI then generates a tile). It is only
    /// returned while it was resolved for the station that <see cref="Status"/> currently describes, so a card
    /// never pairs one station's name with another's image, even when another circuit changed the status.
    /// </summary>
    public string? StationImageUrl => stationImage is { } resolved && string.Equals(resolved.Key, StationKey(Status), StringComparison.Ordinal)
        ? resolved.Url
        : null;
    public BloodSugarStatus? BloodSugar { get; private set; }
    public TemperatureStatus? Temperature { get; private set; }
    public ForecastStatus? Forecast { get; private set; }

    /// <summary>Today's anchor completion per person; empty when nobody has anchors today.</summary>
    public IReadOnlyList<AnchorProgress> Anchors { get; private set; } = [];
    public bool BloodSugarEnabled { get; }
    public bool TemperatureEnabled { get; }
    public bool ForecastEnabled { get; }
    public bool AnchorsEnabled { get; }
    public bool PhotosEnabled => photoLibrary.Count > 0;
    public GlucoseTone GlucoseTone => GlucoseToneMap.From(BloodSugar?.CurrentReading?.Sgv);

    public async Task StartAsync()
    {
        bool alreadyStarted;
        lock (lifecycleGate)
        {
            if (disposed && !started)
            {
                return;
            }

            alreadyStarted = started;
            if (!started)
            {
                started = true;
                homeSpeaker.StatusChanged += onPlayerEvent;
                homeSpeaker.QueueChanged += onQueueChanged;
                playerState.StateChanged += onPlayerStateChanged;
            }
        }

        if (alreadyStarted)
        {
            // A page visited later in the same circuit: anchors may have just been ticked off elsewhere, so
            // refresh them now rather than waiting for the next health tick. The timers are already running.
            await refreshAnchorsAsync();
            raiseChanged("anchor refresh");
            return;
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
            // Resolve the art before publishing the status, so the card gets both in the same update.
            stationImage = await resolveStationImageAsync(status);
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

        if (ForecastEnabled)
        {
            Forecast = ForecastMerge.Latest(Forecast, await tryGetAsync(() => forecastService.GetForecastStatusAsync(), "forecast"));
        }

        await refreshAnchorsAsync();

        raiseChanged("health refresh");
    }

    private async Task refreshAnchorsAsync()
    {
        if (disposed || !AnchorsEnabled)
        {
            return;
        }

        // The circuit's own scoped DbContext may be mid-query for the page, and a DbContext is single-threaded,
        // so this reads through a scope of its own.
        Anchors = await tryGetAsync(async () =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<AnchorService>().GetTodayProgressAsync();
        }, "anchors") ?? Anchors;
    }

    private void raiseChanged(string after)
    {
        if (!disposed)
        {
            try
            {
                Changed?.Invoke();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Dashboard Changed subscriber threw after {After}", after);
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
        if (disposed)
        {
            return;
        }

        // Another circuit (or the queue page) may have published a different station. Until its art is resolved,
        // StationImageUrl is null, so the card shows the new name over its generated tile, never the old art.
        if (!string.Equals(stationImage?.Key, StationKey(Status), StringComparison.Ordinal))
        {
            _ = resolveForCurrentStatusAsync();
        }

        Changed?.Invoke();
    }

    private async Task resolveForCurrentStatusAsync()
    {
        try
        {
            var status = Status;
            var resolved = await resolveStationImageAsync(status);
            if (disposed || !string.Equals(resolved.Key, StationKey(Status), StringComparison.Ordinal))
            {
                return;
            }

            stationImage = resolved;
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Dashboard station image refresh failed");
        }
    }

    private async Task<StationImage> resolveStationImageAsync(SharedPlayerStatus? status) =>
        new(StationKey(status), status is { IsStream: true }
            ? await homeSpeaker.GetStationImageUrlAsync(status.CurrentSong?.Path, status.StreamName)
            : null);

    /// <summary>
    /// Identifies the station a status describes (its stream URL and name), or null when it is not a stream.
    /// <see cref="StationImageUrl"/> is only shown while its key equals the current status' key.
    /// </summary>
    public static string? StationKey(SharedPlayerStatus? status) =>
        status is { IsStream: true } ? string.Concat(status.CurrentSong?.Path, "|", status.StreamName) : null;
}

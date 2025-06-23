using Microsoft.JSInterop;
using HomeSpeaker.WebAssembly.Models;
using HomeSpeaker.WebAssembly.Services;

namespace HomeSpeaker.WebAssembly.Services;

public interface IBrowserAudioService
{
    Task PlaySongAsync(SongViewModel song);
    Task PauseAsync();
    Task ResumeAsync();
    Task StopAsync();
    Task SetVolumeAsync(float volume);
    Task<float> GetVolumeAsync();
    Task SeekToAsync(double seconds);
    Task<BrowserPlayerStatus> GetStatusAsync();
    event EventHandler<BrowserPlayerStatus>? StatusChanged;
    event EventHandler<string>? ErrorOccurred;
}

public class BrowserAudioService : IBrowserAudioService, IAsyncDisposable
{
    private readonly IJSRuntime jsRuntime;
    private readonly ILogger<BrowserAudioService> logger;
    private readonly IConfiguration configuration;
    private IJSObjectReference? audioModule;
    private DotNetObjectReference<BrowserAudioService>? dotNetRef;

    public event EventHandler<BrowserPlayerStatus>? StatusChanged;
    public event EventHandler<string>? ErrorOccurred;

    public BrowserAudioService(IJSRuntime jsRuntime, ILogger<BrowserAudioService> logger, IConfiguration configuration)
    {
        this.jsRuntime = jsRuntime;
        this.logger = logger;
        this.configuration = configuration;
    }

    private async Task EnsureInitializedAsync()
    {
        if (audioModule == null)
        {
            audioModule = await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/audioPlayer.js");
            dotNetRef = DotNetObjectReference.Create(this);
            await audioModule.InvokeVoidAsync("initialize", dotNetRef);
        }
    }

    public async Task PlaySongAsync(SongViewModel song)
    {        try
        {
            Console.WriteLine($"BrowserAudioService.PlaySongAsync called with song: {song.Name}");
            await EnsureInitializedAsync();
            // Get the current base address from the browser
            var baseUri = await jsRuntime.InvokeAsync<string>("eval", "window.location.origin");
            var audioUrl = $"{baseUri}/api/music/{song.SongId}";
            
            Console.WriteLine($"Playing song {song.Name} from {audioUrl}");
            logger.LogInformation("Playing song {songName} from {url}", song.Name, audioUrl);
            await audioModule!.InvokeVoidAsync("playSong", audioUrl, song.Name);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in BrowserAudioService.PlaySongAsync: {ex}");
            logger.LogError(ex, "Error playing song {songName}", song.Name);
            ErrorOccurred?.Invoke(this, $"Failed to play {song.Name}: {ex.Message}");
        }
    }

    public async Task PauseAsync()
    {
        try
        {
            await EnsureInitializedAsync();
            await audioModule!.InvokeVoidAsync("pause");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error pausing audio");
            ErrorOccurred?.Invoke(this, $"Failed to pause: {ex.Message}");
        }
    }

    public async Task ResumeAsync()
    {
        try
        {
            await EnsureInitializedAsync();
            await audioModule!.InvokeVoidAsync("resume");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resuming audio");
            ErrorOccurred?.Invoke(this, $"Failed to resume: {ex.Message}");
        }
    }

    public async Task StopAsync()
    {
        try
        {
            await EnsureInitializedAsync();
            await audioModule!.InvokeVoidAsync("stop");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping audio");
            ErrorOccurred?.Invoke(this, $"Failed to stop: {ex.Message}");
        }
    }

    public async Task SetVolumeAsync(float volume)
    {
        try
        {
            await EnsureInitializedAsync();
            await audioModule!.InvokeVoidAsync("setVolume", Math.Max(0, Math.Min(1, volume)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting volume");
            ErrorOccurred?.Invoke(this, $"Failed to set volume: {ex.Message}");
        }
    }

    public async Task<float> GetVolumeAsync()
    {
        try
        {
            await EnsureInitializedAsync();
            return await audioModule!.InvokeAsync<float>("getVolume");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting volume");
            ErrorOccurred?.Invoke(this, $"Failed to get volume: {ex.Message}");
            return 0.5f;
        }
    }

    public async Task SeekToAsync(double seconds)
    {
        try
        {
            await EnsureInitializedAsync();
            await audioModule!.InvokeVoidAsync("seekTo", seconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error seeking audio");
            ErrorOccurred?.Invoke(this, $"Failed to seek: {ex.Message}");
        }
    }

    public async Task<BrowserPlayerStatus> GetStatusAsync()
    {
        try
        {
            await EnsureInitializedAsync();
            return await audioModule!.InvokeAsync<BrowserPlayerStatus>("getStatus");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting status");
            ErrorOccurred?.Invoke(this, $"Failed to get status: {ex.Message}");
            return new BrowserPlayerStatus();
        }
    }

    [JSInvokable]
    public void OnStatusChanged(BrowserPlayerStatus status)
    {
        StatusChanged?.Invoke(this, status);
    }

    [JSInvokable]
    public void OnError(string error)
    {
        logger.LogError("Browser audio error: {error}", error);
        ErrorOccurred?.Invoke(this, error);
    }

    public async ValueTask DisposeAsync()
    {
        if (audioModule != null)
        {
            await audioModule.InvokeVoidAsync("dispose");
            await audioModule.DisposeAsync();
        }
        
        dotNetRef?.Dispose();
    }
}

public class BrowserPlayerStatus
{
    public bool IsPlaying { get; set; }
    public bool IsPaused { get; set; }
    public double CurrentTime { get; set; }
    public double Duration { get; set; }
    public float Volume { get; set; }
    public string? CurrentSong { get; set; }
    public double PercentComplete => Duration > 0 ? (CurrentTime / Duration) * 100 : 0;
}

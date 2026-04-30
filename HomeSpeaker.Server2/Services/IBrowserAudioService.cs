using HomeSpeaker.Server2.Models;

namespace HomeSpeaker.Server2.Services;

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

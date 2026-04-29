using HomeSpeaker.WebAssembly.Models;

namespace HomeSpeaker.WebAssembly.Services;

public interface IPlaybackModeService
{
    PlaybackMode CurrentMode { get; set; }
    Task PlaySongAsync(SongViewModel song);
    Task PauseAsync();
    Task ResumeAsync();
    Task StopAsync();
    Task SetVolumeAsync(int volume);
    Task<int> GetVolumeAsync();
    event EventHandler<object>? ModeChanged;
    event EventHandler<string>? StatusMessage;
}

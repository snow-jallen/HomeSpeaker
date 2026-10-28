using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Services;

/// <summary>
/// Builds the <see cref="PlayerStatusDto"/> served by the status endpoint, the SSE stream and
/// Live Activity pushes, so all three describe the player the same way.
/// </summary>
public sealed class PlayerStatusProvider : IPlayerStatusProvider
{
    private readonly IMusicPlayer musicPlayer;

    public PlayerStatusProvider(IMusicPlayer musicPlayer)
    {
        this.musicPlayer = musicPlayer;
    }

    public async Task<PlayerStatusDto> GetAsync(CancellationToken cancellationToken)
    {
        var status = musicPlayer.Status;
        var volume = await musicPlayer.GetVolume();

        return new PlayerStatusDto
        {
            Elapsed = status.Elapsed,
            Remaining = status.Remaining,
            StillPlaying = musicPlayer.StillPlaying,
            PercentComplete = status.PercentComplete,
            CurrentSong = status.CurrentSong,
            Volume = volume,
            SleepTimerActive = musicPlayer.SleepTimerActive,
            SleepTimerRemainingMinutes = musicPlayer.SleepTimerRemaining?.TotalMinutes,
            RepeatMode = musicPlayer.RepeatMode,
            IsStream = status.IsStream,
            StreamName = status.IsStream ? status.StreamName : null
        };
    }
}

using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Services;

/// <summary>
/// Builds the <see cref="PlayerStatusDto"/> served by the status endpoint, the SSE stream and
/// Live Activity pushes, so all three describe the player the same way.
/// </summary>
public sealed class PlayerStatusProvider : IPlayerStatusProvider
{
    private readonly IMusicPlayer musicPlayer;
    private readonly IServiceScopeFactory scopeFactory;

    public PlayerStatusProvider(IMusicPlayer musicPlayer, IServiceScopeFactory scopeFactory)
    {
        this.musicPlayer = musicPlayer;
        this.scopeFactory = scopeFactory;
    }

    public async Task<PlayerStatusDto> GetAsync(CancellationToken cancellationToken)
    {
        var status = musicPlayer.Status;
        var volume = await musicPlayer.GetVolume();

        // AiPlaybackService is scoped (it holds a DbContext), and this provider is a singleton.
        // Its context lookup is cached in the shared IMemoryCache, so a scope per call is cheap.
        AiPlayerContextDto? aiContext;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var aiPlaybackService = scope.ServiceProvider.GetRequiredService<AiPlaybackService>();
            aiContext = await aiPlaybackService.GetCurrentContextAsync(cancellationToken);
        }

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
            AiContext = aiContext,
            IsStream = status.IsStream,
            StreamName = status.IsStream ? status.StreamName : null
        };
    }
}

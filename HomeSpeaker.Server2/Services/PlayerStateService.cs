using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Services;

/// <summary>
/// Tracks player state for Blazor components. Components can query IMusicPlayer directly for real-time state.
/// </summary>
public class PlayerStateService
{
    private PlayerStatus? status;
    private bool repeatMode;

    public PlayerStatus? Status => status;
    public bool RepeatMode => repeatMode;

    public event Action? StateChanged;

    public void UpdateStatus(PlayerStatus? status)
    {
        this.status = status;
        StateChanged?.Invoke();
    }

    public void UpdateRepeatMode(bool repeatMode)
    {
        this.repeatMode = repeatMode;
        StateChanged?.Invoke();
    }
}

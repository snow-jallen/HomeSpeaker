using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Services;

// Minimal PlayerStateService for compatibility with migrated UI components
// In server mode, components can query IMusicPlayer directly for real-time state
public class PlayerStateService
{
    private GetStatusReply? status;
    private bool repeatMode;

    public GetStatusReply? Status => status;
    public bool RepeatMode => repeatMode;

    public event Action? StateChanged;

    public void UpdateStatus(GetStatusReply? status)
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

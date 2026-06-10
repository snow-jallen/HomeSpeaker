using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Services;

public class PlayerStateService
{
    private PlayerStatus? status;
    private bool repeatMode;
    private int volume = 50;

    public PlayerStatus? Status => status;
    public bool RepeatMode => repeatMode;
    public int Volume => volume;

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

    public void UpdateVolume(int newVolume)
    {
        if (volume != newVolume)
        {
            volume = newVolume;
            StateChanged?.Invoke();
        }
    }
}

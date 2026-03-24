namespace HomeSpeaker.WebAssembly.Services;

public class PlayerStateService
{
    private GetStatusReply? _status;
    private bool _repeatMode;

    public GetStatusReply? Status => _status;
    public bool RepeatMode => _repeatMode;

    public event Action? StateChanged;

    public void UpdateStatus(GetStatusReply? status)
    {
        _status = status;
        StateChanged?.Invoke();
    }

    public void UpdateRepeatMode(bool repeatMode)
    {
        _repeatMode = repeatMode;
        StateChanged?.Invoke();
    }
}

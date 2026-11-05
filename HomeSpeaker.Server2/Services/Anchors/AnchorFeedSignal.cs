namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>Nudges the feed sender to send queued ticks now rather than at its next check.</summary>
public sealed class AnchorFeedSignal
{
    public event Action? Woken;

    public void Wake() => Woken?.Invoke();
}

namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>Tells open pages that anchors changed, so a tick on one screen shows on every other one.</summary>
public sealed class AnchorChangeNotifier
{
    public event Action? Changed;

    /// <summary>True while some page is open and listening, so pollers can rest when nobody is looking.</summary>
    public bool HasListeners => Changed is not null;

    public void NotifyChanged() => Changed?.Invoke();
}

namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>A person's anchors on one day: how many were expected (open, kept or missed) and how many were kept.</summary>
public sealed record AnchorDayProgress(int Done, int Total);

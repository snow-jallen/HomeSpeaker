namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>How one anchor stands on one date. Wire values are the lowercase names.</summary>
public enum AnchorCell
{
    /// <summary>Logged done (even on a day it wasn't expected).</summary>
    Kept,

    /// <summary>Expected on a past day and not done.</summary>
    Missed,

    /// <summary>Expected today and not done yet.</summary>
    Open,

    /// <summary>Active, but that weekday isn't scheduled.</summary>
    Off,

    /// <summary>Outside every active period (before it existed, while retired, or in the future).</summary>
    None,

    /// <summary>Expected on a past day, no log, and the source says a missing log means "no data", not a miss.</summary>
    Unknown,
}

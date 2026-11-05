namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>What happened to a tick sent to an external feed.</summary>
public enum FeedWriteOutcome
{
    /// <summary>The feed took it (or ignored it as older than what it has).</summary>
    Sent,

    /// <summary>The feed refused it for good (unknown or unshared anchor, date out of range): drop it.</summary>
    Rejected,

    /// <summary>The feed couldn't be reached or had a problem: try again later.</summary>
    Failed,
}
